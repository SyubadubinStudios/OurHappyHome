using System.Numerics;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

public sealed partial class GameSession
{
    public const float WalkSpeed = 1.45f;
    public const float RunSpeed = 3.3f;

    private int _collisionRevision = -1;

    /// <summary>Where the family car parks before the garage is built.</summary>
    public static readonly Vector2 DrivewayCar = new(11.5f, 10.8f);

    private static readonly Rect NavArea = Rooms.Lot.Inflate(1.5f);

    private void RebuildCollisionIfNeeded()
    {
        int revision = (State.House.Revision * 100_000) + State.House.FurnitureRevision;
        if (revision != _collisionRevision)
        {
            RebuildCollision();
        }
    }

    /// <summary>House walls and furniture are dynamic obstacles; rebuilt with the nav grid on every change.</summary>
    public void RebuildCollision()
    {
        House house = State.House;
        List<Rect> rects = [];
        foreach (WallSegment wall in house.Walls)
        {
            rects.Add(wall.Bounds);
        }

        foreach (FurnitureItem item in house.Furniture)
        {
            FurnitureDef def = item.Def;
            if (def.Height <= 0.05f || def.WallMounted)
            {
                continue;
            }

            Rect bounds = item.Bounds;
            if (def.Id == "tree")
            {
                bounds = Rect.FromCenter(item.Position, new Vector2(0.7f, 0.7f));
            }

            rects.Add(bounds.Inflate(-0.04f));
        }

        if (!house.Has(RoomId.Garage))
        {
            rects.Add(Rect.FromCenter(DrivewayCar, new Vector2(1.8f, 2.9f)));
        }

        if (house.Has(RoomId.Pool))
        {
            Rect pool = Rooms.Get(RoomId.Pool).Area;
            rects.Add(Rect.FromCenter(pool.Center, pool.Size - new Vector2(1.2f, 1.2f)));
        }

        if (house.Has(RoomId.TreeHouse))
        {
            rects.Add(Rect.FromCenter(Rooms.Get(RoomId.TreeHouse).Area.Center, new Vector2(1.4f, 1.4f)));
        }

        Collision.SetDynamic(rects);
        HomeNav = new NavGrid(NavArea, 0.25f, Collision, CharacterRadius);
        _collisionRevision = (house.Revision * 100_000) + house.FurnitureRevision;
    }

    // --------------------------------------------------------------- player

    private void MoveControlled(FamilyMember m, float realDt)
    {
        Vector2 input = PlayerMove;
        if (input.LengthSquared() < 0.01f)
        {
            m.Moving = false;
            m.Running = false;
            return;
        }

        // Moving cancels whatever the controlled character was doing.
        if (m.Task is not null)
        {
            if (m.Task.Phase == TaskPhase.Performing || m.Task.FromPlayer)
            {
                CancelTask(m);
            }
            else
            {
                CancelTask(m);
            }
        }

        if (input.LengthSquared() > 1f)
        {
            input = Vector2.Normalize(input);
        }

        bool run = PlayerRun && m.Stamina.CanRun;
        float speed = (run ? RunSpeed : WalkSpeed) * SpeedModifier(m);
        if (run)
        {
            m.Stamina.Value -= 4.5f * realDt;
        }

        Vector2 before = m.Position;
        m.Position = Collision.Move(m.Position, input * speed * realDt, CharacterRadius);
        m.Moving = Vector2.DistanceSquared(before, m.Position) > 1e-7f;
        m.Running = run && m.Moving;
        m.Yaw = TurnTowards(m.Yaw, MathF.Atan2(input.X, input.Y), realDt * 12f);
        OnPlayerMoved(m);
    }

    /// <summary>Slower when exhausted, sick or carrying someone who is down.</summary>
    public float SpeedModifier(FamilyMember m)
    {
        float modifier = 1f;
        if (m.Stamina.State == StaminaState.MustRecover)
        {
            modifier *= 0.6f;
        }
        else if (m.Stamina.State == StaminaState.Exhausted)
        {
            modifier *= 0.8f;
        }

        if (m.Sick)
        {
            modifier *= 0.85f;
        }

        if (m.IsChild)
        {
            modifier *= 0.95f;
        }

        if (State.Members.Any(o => o.Safety == SafetyState.Following && o.FollowTarget == m.Id && o.Needs[NeedKind.Health] < 40))
        {
            modifier *= 0.75f;
        }

        if (State.Inventory.Has("bicycle") && m.Id == MemberId.Player && !State.House.IsIndoors(m.Position) && !Rooms.Lot.Contains(m.Position))
        {
            modifier *= 1.8f;
        }

        return modifier;
    }

    private void OnPlayerMoved(FamilyMember m)
    {
        PlaceId? place = Map.PlaceAt(m.Position);
        if (place is { } p && p != CurrentPlace)
        {
            CurrentPlace = p;
            OnEnterPlace(p);
        }
        else if (place is null)
        {
            CurrentPlace = null;
        }
    }

    public PlaceId? CurrentPlace { get; private set; } = PlaceId.Home;

    // ----------------------------------------------------------- characters

    private void UpdateMember(FamilyMember m, float minutes, float realDt, float speed)
    {
        if (m.Away)
        {
            m.Moving = false;
            return;
        }

        // Rescue states: frozen in place until someone helps.
        if (m.InDanger)
        {
            m.Moving = false;
            if (m.Task is not null)
            {
                CancelTask(m);
            }

            return;
        }

        if (m.Safety == SafetyState.Following && m.FollowTarget is { } leader)
        {
            FollowLeader(m, State.Member(leader), realDt);
            return;
        }

        bool controlled = m.Id == State.Controlled;
        if (controlled)
        {
            if (PlayerMove.LengthSquared() > 0.01f || m.Task is null)
            {
                MoveControlled(m, realDt);
            }

            if (m.Task is not null)
            {
                UpdateTask(m, minutes, realDt, 1f);
            }

            return;
        }

        if (State.Party.Contains(m.Id) && !Rooms.Lot.Contains(Controlled.Position))
        {
            // On a trip the family stays close to the player.
            FollowLeader(m, Controlled, realDt);
            return;
        }

        if (m.Task is null)
        {
            m.IdleTimer -= minutes;
            if (m.IdleTimer <= 0f)
            {
                ChooseNextActivity(m);
                m.IdleTimer = Random.Range(0.5f, 2f);
            }

            m.Moving = false;
            return;
        }

        UpdateTask(m, minutes, realDt, MathF.Min(speed, 5f));
    }

    private void FollowLeader(FamilyMember m, FamilyMember leader, float realDt)
    {
        if (m.Task is not null)
        {
            CancelTask(m);
        }

        int index = Array.IndexOf(FamilyNames.All, m.Id);
        Vector2 back = new(-MathF.Sin(leader.Yaw), -MathF.Cos(leader.Yaw));
        Vector2 side = new(back.Y, -back.X);
        Vector2 slot = leader.Position + (back * (1.1f + (0.5f * (index % 3)))) + (side * (((index % 2) * 2) - 1) * 0.7f);
        Vector2 to = slot - m.Position;
        float distance = to.Length();
        if (distance > 25f)
        {
            m.Position = slot;
            m.Moving = false;
            return;
        }

        if (distance < 0.35f)
        {
            m.Moving = false;
            m.Yaw = TurnTowards(m.Yaw, leader.Yaw, realDt * 6f);
            return;
        }

        // Walls in the way: follow a path through the doorways instead of pushing into them.
        Vector2 aim = slot;
        m.RepathTimer -= realDt;
        if (!Collision.LineClear(m.Position, slot, CharacterRadius * 0.9f))
        {
            if (m.FollowPath is null || m.RepathTimer <= 0f)
            {
                m.FollowPath = FindPath(m.Position, leader.Position);
                m.RepathTimer = 0.6f;
            }

            if (m.FollowPath is { Count: > 0 } path)
            {
                while (path.Count > 1 && Vector2.Distance(path[0], m.Position) < 0.35f)
                {
                    path.RemoveAt(0);
                }

                aim = path[0];
            }
        }
        else
        {
            m.FollowPath = null;
        }

        Vector2 toAim = aim - m.Position;
        float aimDistance = MathF.Max(toAim.Length(), 1e-4f);
        bool run = distance > 3f;
        float speed = (run ? RunSpeed : WalkSpeed) * 1.05f;
        Vector2 step = toAim / aimDistance * MathF.Min(aimDistance, speed * realDt);
        m.Position = Collision.Move(m.Position, step, CharacterRadius);
        m.Moving = true;
        m.Running = run;
        m.Yaw = TurnTowards(m.Yaw, MathF.Atan2(toAim.X, toAim.Y), realDt * 10f);
    }

    /// <summary>Moves along the task's path; returns true on arrival.</summary>
    private bool WalkTask(FamilyMember m, MemberTask task, float moveDt)
    {
        if (task.Path is null)
        {
            task.Path = FindPath(m.Position, task.Target) ?? [task.Target];
            task.PathIndex = 0;
            if (task.WalkBudget <= 0f)
            {
                float length = 0f;
                Vector2 previous = m.Position;
                foreach (Vector2 point in task.Path)
                {
                    length += Vector2.Distance(previous, point);
                    previous = point;
                }

                task.WalkBudget = 4f + (length / (WalkSpeed * 0.6f));
            }
        }

        // A walk that takes far longer than it should: give up and appear there.
        task.WalkTime += moveDt;
        if (task.WalkTime > task.WalkBudget)
        {
            m.Position = task.Target;
            m.Moving = false;
            return true;
        }

        if (task.PathIndex >= task.Path.Count)
        {
            m.Moving = false;
            return true;
        }

        Vector2 waypoint = task.Path[task.PathIndex];
        Vector2 to = waypoint - m.Position;
        float distance = to.Length();
        bool last = task.PathIndex == task.Path.Count - 1;
        if (distance < (last ? 0.08f : 0.3f))
        {
            task.PathIndex++;
            if (task.PathIndex >= task.Path.Count)
            {
                m.Moving = false;
                return true;
            }

            return false;
        }

        bool run = task.Run && m.Stamina.CanRun;
        float speed = (run ? RunSpeed : WalkSpeed) * SpeedModifier(m);
        float stepLength = MathF.Min(distance, speed * moveDt);
        Vector2 before = m.Position;
        Vector2 desired = to / distance * stepLength;
        m.Position = last && distance < 0.6f ? m.Position + desired : Collision.Move(m.Position, desired, CharacterRadius * 0.9f);
        float moved = Vector2.Distance(before, m.Position);
        m.Moving = true;
        m.Running = run;
        if (run)
        {
            m.Stamina.Value -= 3f * moveDt;
        }

        m.Yaw = TurnTowards(m.Yaw, MathF.Atan2(to.X, to.Y), moveDt * 10f);

        // Stuck behind something: try a fresh path, then just appear there.
        if (moved < stepLength * 0.2f)
        {
            m.StuckTimer += moveDt;
            if (m.StuckTimer > 1.2f && m.StuckTimer < 1.3f)
            {
                task.Path = null;
            }
            else if (m.StuckTimer > 3f)
            {
                m.Position = task.Target;
                m.StuckTimer = 0f;
                return true;
            }
        }
        else
        {
            m.StuckTimer = 0f;
        }

        return false;
    }

    public List<Vector2>? FindPath(Vector2 from, Vector2 to)
    {
        if (HomeNav is { } nav && nav.Contains(from) && nav.Contains(to))
        {
            return nav.FindPath(from, to, Collision, CharacterRadius);
        }

        return [to];
    }

    public static float TurnTowards(float current, float target, float maxStep)
    {
        float delta = target - current;
        while (delta > MathF.PI)
        {
            delta -= MathF.Tau;
        }

        while (delta < -MathF.PI)
        {
            delta += MathF.Tau;
        }

        return current + Math.Clamp(delta, -maxStep, maxStep);
    }

    /// <summary>Places the player (and the party) somewhere else, e.g. after choosing a destination on the map.</summary>
    public void Travel(PlaceId destination, bool bringFamily)
    {
        Place place = Map.Get(destination);
        FamilyMember player = Controlled;
        CancelTask(player);
        player.Position = place.Entrance;
        player.Yaw = place.EntranceYaw;
        State.Party.Clear();
        if (bringFamily)
        {
            foreach (FamilyMember m in State.Members.Where(m => m.Id != player.Id && !m.Away && !m.InDanger))
            {
                State.Party.Add(m.Id);
                CancelTask(m);
                m.Position = place.Entrance + new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f));
            }
        }

        // Anyone left out on an earlier trip heads home.
        foreach (FamilyMember m in State.Members.Where(m => m.Id != player.Id && !m.Away && !State.Party.Contains(m.Id) && !Rooms.Lot.Contains(m.Position)))
        {
            CancelTask(m);
            m.Safety = SafetyState.Normal;
            m.FollowTarget = null;
            m.Position = Rooms.FrontDoor + new Vector2(Random.Range(-1f, 1f), 1.6f);
        }

        // Travelling takes time: farther places take longer.
        float distance = Vector2.Distance(Rooms.FrontDoor, place.Entrance);
        double travelMinutes = Math.Clamp(distance / 8f, 5f, 45f);
        Clock.Advance(travelMinutes);
        CurrentPlace = destination;
        OnEnterPlace(destination);
        if (destination == PlaceId.Home)
        {
            State.Party.Clear();
        }
    }

    // -------------------------------------------------------------- interiors

    /// <summary>The building the controlled member is inside, if any.</summary>
    public Interior? CurrentInterior => Map.InteriorAt(Controlled.Position);

    /// <summary>Walks the controlled member (and the party) through the door of a town building.</summary>
    public void EnterInterior(PlaceId place)
    {
        if (Map.InteriorFor(place) is not { } interior)
        {
            return;
        }

        MoveGroup(interior.Spawn, interior.SpawnYaw);
        Bus.Sound("door");
        Bus.Notice(Loc.T($"Masuk ke {interior.Name}", $"Entered the {interior.Name}"), WorldMap.Icon(place), NoticeKind.Info);
    }

    /// <summary>Back out to the street in front of the building.</summary>
    public void ExitInterior()
    {
        if (CurrentInterior is not { } interior)
        {
            return;
        }

        Place place = Map.Get(interior.Place);
        float yaw = place.EntranceYaw + MathF.PI;
        MoveGroup(place.Entrance, yaw);
        Bus.Sound("door");
    }

    private void MoveGroup(Vector2 position, float yaw)
    {
        FamilyMember player = Controlled;
        CancelTask(player);
        player.Position = position;
        player.Yaw = yaw;
        Vector2 back = -new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
        Vector2 side = new(back.Y, -back.X);
        int i = 0;
        foreach (FamilyMember m in State.Members.Where(m => State.Party.Contains(m.Id) && !m.Away && !m.InDanger))
        {
            // Side by side with the player, so nobody blocks the camera's view.
            CancelTask(m);
            m.Position = position + (side * ((i % 2 == 0 ? 1f : -1f) * (0.9f + (0.8f * (i / 2))))) - (back * 0.2f);
            m.Yaw = yaw;
            i++;
        }

        foreach (Pet pet in State.Pets.Where(p => p.State == PetState.Follow))
        {
            pet.Position = position - (side * 1.1f) - (back * 0.4f);
        }

        Bus.Publish(new TeleportEvent(position, yaw));
    }

    // ------------------------------------------------------------------- pets

    private void UpdatePets(float minutes, float realDt, float speed)
    {
        foreach (Pet pet in State.Pets)
        {
            pet.Tick(minutes);
            pet.BarkCooldown -= realDt;
            pet.StateTimer -= minutes;
            FamilyMember player = Controlled;

            if (pet.StateTimer <= 0f)
            {
                ChoosePetState(pet, player);
            }

            Vector2 target = pet.State switch
            {
                PetState.Follow => player.Position - (new Vector2(MathF.Sin(player.Yaw), MathF.Cos(player.Yaw)) * 1.3f),
                _ => pet.Target,
            };

            Vector2 to = target - pet.Position;
            float distance = to.Length();
            if (distance > 30f)
            {
                pet.Position = target;
                pet.Moving = false;
            }
            else if (distance > 0.4f && pet.State is PetState.Follow or PetState.Wander or PetState.Play)
            {
                float petSpeed = (pet.State == PetState.Play || distance > 4f ? 3.2f : 1.3f) * MathF.Min(speed, 4f);
                pet.Position = Collision.Move(pet.Position, to / distance * MathF.Min(distance, petSpeed * realDt), 0.22f);
                pet.Yaw = TurnTowards(pet.Yaw, MathF.Atan2(to.X, to.Y), realDt * 8f);
                pet.Moving = true;
            }
            else
            {
                pet.Moving = false;
            }
        }
    }

    private void ChoosePetState(Pet pet, FamilyMember player)
    {
        bool night = Hour >= 21f || Hour < 6f;
        if (night || pet.Energy < 15f)
        {
            pet.State = PetState.Sleep;
            FurnitureItem? bed = State.House.Furniture.FirstOrDefault(f => f.DefId == "dog-bed");
            pet.Target = bed?.Position ?? new Vector2(-2.5f, 2.6f);
            pet.Position = pet.Target;
            pet.StateTimer = 60f;
            return;
        }

        float roll = Random.NextFloat();
        if (roll < 0.45f && Vector2.Distance(player.Position, pet.Position) < 40f)
        {
            pet.State = PetState.Follow;
            pet.StateTimer = Random.Range(10f, 30f);
        }
        else if (roll < 0.8f)
        {
            pet.State = PetState.Wander;
            Vector2 around = Rooms.Lot.Contains(pet.Position) ? pet.Position : player.Position;
            for (int i = 0; i < 6; i++)
            {
                Vector2 candidate = around + new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f));
                if (!Collision.Blocked(candidate, 0.25f))
                {
                    pet.Target = candidate;
                    break;
                }
            }

            pet.StateTimer = Random.Range(4f, 12f);
        }
        else
        {
            pet.State = PetState.Idle;
            pet.Target = pet.Position;
            pet.StateTimer = Random.Range(5f, 15f);
        }
    }

    /// <summary>Pets notice unusual activity: a dog barks at strangers and wild animals.</summary>
    public void PetAlert(Vector2 source, string reason)
    {
        foreach (Pet pet in State.Pets.Where(p => p.Kind == PetKind.Dog))
        {
            if (pet.BarkCooldown > 0f)
            {
                continue;
            }

            pet.BarkCooldown = 6f;
            pet.State = PetState.Bark;
            pet.StateTimer = 3f;
            pet.Yaw = MathF.Atan2(source.X - pet.Position.X, source.Y - pet.Position.Y);
            Bus.Sound("bark", new Vector3(pet.Position.X, 0.5f, pet.Position.Y));
            Bus.Notice(Loc.T($"{pet.Name} menggonggong! {reason}", $"{pet.Name} is barking! {reason}"), "🐶", NoticeKind.Warning);
        }
    }

    // -------------------------------------------------------------- neighbours

    private void UpdateNpcs(float realDt)
    {
        foreach (Npc npc in Npcs)
        {
            npc.Update(realDt, Random);
        }
    }
}

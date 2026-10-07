using System.Numerics;
using OurHappyHome.Core.Cooking;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Progression;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;

namespace OurHappyHome.Core;

public sealed partial class GameSession
{
    public readonly record struct FurnitureChoice(FurnitureItem Item, int Slot);

    /// <summary>Starts a task: walk to the furniture slot (or target point), then perform.</summary>
    public MemberTask StartTask(FamilyMember m, ActivityId activity, FurnitureItem? item, int slot, Vector2? target = null,
        bool run = false, bool fromPlayer = false, string tag = "", string? recipe = null, MemberId? partner = null, float? minutes = null)
    {
        CancelTask(m);
        ActivityDef def = ActivityCatalog.Get(activity);
        MemberTask task = new()
        {
            Activity = activity,
            FurnitureUid = item?.Uid,
            Slot = slot,
            Run = run,
            FromPlayer = fromPlayer,
            Tag = tag,
            RecipeId = recipe,
            Partner = partner,
            Total = minutes ?? def.Minutes,
        };
        task.Remaining = task.Total;

        if (item is not null && slot >= 0)
        {
            item.EnsureOccupants();
            item.Occupants[slot] = new MemberSlot(m.Id);
            task.Target = item.ApproachPoint(slot);
            task.TargetYaw = item.SlotWorldYaw(slot);
        }
        else
        {
            task.Target = target ?? m.Position;
        }

        m.Task = task;
        m.StuckTimer = 0f;
        return task;
    }

    public void CancelTask(FamilyMember m)
    {
        if (m.Task is { } task && task.FurnitureUid is { } uid && State.House.Find(uid) is { } item)
        {
            item.Release(m.Id);
            if (m.Anchor is not null)
            {
                // Step off the furniture to its approach point.
                m.Position = item.ApproachPoint(Math.Max(0, task.Slot));
            }
        }

        if (PendingMiniGame is not null && m.Id == State.Controlled && m.Task?.Activity == ActivityId.Cook)
        {
            PendingMiniGame = null;
        }

        m.Task = null;
        m.Anchor = null;
        m.Pose = AnchorPose.Stand;
        m.Moving = false;
    }

    private void UpdateTask(FamilyMember m, float minutes, float realDt, float moveScale)
    {
        MemberTask task = m.Task!;
        FurnitureItem? item = task.FurnitureUid is { } uid ? State.House.Find(uid) : null;
        if (task.FurnitureUid is not null && (item is null || (item.Broken && task.Activity != ActivityId.Repair)))
        {
            CancelTask(m);
            return;
        }

        if (task.Phase == TaskPhase.Walking)
        {
            float moveDt = realDt * moveScale;
            if (EffectiveSpeed > 10f && m.Id != State.Controlled)
            {
                m.Position = task.Target; // night skip: no need to watch them walk
            }
            else if (!WalkTask(m, task, moveDt))
            {
                return;
            }

            ArriveAtTask(m, task, item);
            return;
        }

        if (task.Phase != TaskPhase.Performing)
        {
            return;
        }

        // Cooking by the player waits for the mini-game result.
        if (task.Activity == ActivityId.Cook && task.FromPlayer && task.MiniGameScore is null)
        {
            return;
        }

        ActivityDef def = task.Def;
        float rateScale = 1f;
        if (task.Activity == ActivityId.Eat && task.Tag.StartsWith("quality:", StringComparison.Ordinal)
            && Enum.TryParse(task.Tag["quality:".Length..], out CookQuality q))
        {
            rateScale = Recipes.NutritionMultiplier(q);
        }

        foreach ((NeedKind need, float rate) in def.Rates)
        {
            float r = rate;
            if (need == NeedKind.Hunger && task.Activity == ActivityId.Eat)
            {
                r *= rateScale;
            }

            m.Needs.Add(need, r * minutes);
        }

        if (def.StaminaPerMinute < 0)
        {
            m.Stamina.Value += def.StaminaPerMinute * minutes;
        }

        if (def.Skill is { } skill && def.SkillPerMinute > 0)
        {
            SkillKind practiced = skill;
            if (task.Activity == ActivityId.Homework)
            {
                practiced = ((Clock.DayIndex + (int)m.Id) % 3) switch { 0 => SkillKind.Math, 1 => SkillKind.English, _ => SkillKind.Science };
            }
            else if (task.Activity == ActivityId.Read && m.Id == MemberId.YoungerSister)
            {
                practiced = SkillKind.English;
            }

            float bonus = m.Personality.Hobbies.Contains(practiced) ? 1.3f : 1f;
            Practice(m, practiced, def.SkillPerMinute * minutes * bonus * 0.22f);
        }

        if (task.Activity == ActivityId.Chat && task.Partner is { } partner)
        {
            State.Relationships.Change(m.Id, partner, 0.12f * minutes);
        }

        // Sleepers wake up on their own once rested and it is morning.
        if (task.Activity == ActivityId.Sleep && m.Needs[NeedKind.Sleep] >= 96f && Hour >= m.Personality.WakeHour && Hour < 12f)
        {
            task.Remaining = 0f;
        }

        task.Remaining -= minutes;
        if (task.Remaining <= 0f)
        {
            CompleteTask(m, task, item);
        }
    }

    private void ArriveAtTask(FamilyMember m, MemberTask task, FurnitureItem? item)
    {
        task.Phase = TaskPhase.Performing;
        m.Moving = false;
        if (item is not null && task.Slot >= 0 && task.Slot < item.Def.Slots.Length)
        {
            UseSlot slot = item.Def.Slots[task.Slot];
            Vector3 anchor = item.SlotWorldPosition(task.Slot);
            m.Pose = slot.Pose;
            if (slot.Pose == AnchorPose.Stand)
            {
                m.Position = new Vector2(anchor.X, anchor.Z);
                m.Anchor = anchor.Y > 0.05f ? anchor : null;
            }
            else
            {
                m.Anchor = anchor;
                m.Position = new Vector2(anchor.X, anchor.Z);
            }

            m.Yaw = item.SlotWorldYaw(task.Slot);
        }
        else if (task.TargetYaw is { } yaw)
        {
            m.Yaw = yaw;
        }

        OnTaskStarted(m, task, item);
    }

    /// <summary>Side effects when an activity begins (sounds, effects, mini-games).</summary>
    private void OnTaskStarted(FamilyMember m, MemberTask task, FurnitureItem? item)
    {
        Vector3 at = new(m.Position.X, 1.2f, m.Position.Y);
        switch (task.Activity)
        {
            case ActivityId.Cook:
                Bus.Sound("sizzle", at, 0.8f);
                if (task.FromPlayer && task.RecipeId is { } recipe)
                {
                    PendingMiniGame = new MiniGameRequest("cook", recipe);
                    Bus.Publish(PendingMiniGame);
                }

                break;
            case ActivityId.WatchTV:
                Bus.Sound("tv", at, 0.5f);
                break;
            case ActivityId.Shower:
                Bus.Sound("shower", at, 0.6f);
                Bus.Effect(EffectKind.Steam, m.Position, 1.8f);
                break;
            case ActivityId.PlayMusic:
                Bus.Effect(EffectKind.Notes, m.Position, 1.6f);
                Bus.Sound("piano", at);
                break;
            case ActivityId.Sleep:
                Bus.Effect(EffectKind.Zzz, m.Position, 1.0f);
                break;
            case ActivityId.SellLemonade when task.FromPlayer:
                PendingMiniGame = new MiniGameRequest("lemonade", "");
                Bus.Publish(PendingMiniGame);
                break;
        }
    }

    private void CompleteTask(FamilyMember m, MemberTask task, FurnitureItem? item)
    {
        ActivityId activity = task.Activity;
        Vector2 pos = m.Position;
        item?.Release(m.Id);
        if (m.Anchor is not null && item is not null)
        {
            m.Position = item.ApproachPoint(Math.Max(0, task.Slot));
        }

        m.Task = null;
        m.Anchor = null;
        m.Pose = AnchorPose.Stand;

        switch (activity)
        {
            case ActivityId.Cook when task.Tag == "helper":
                Practice(m, SkillKind.Cooking, 0.35f);
                m.Mood.Add("helped-cook", Loc.T("Membantu memasak", "Helped with cooking"), 8, MoodKind.Happy, Now, 180);
                break;
            case ActivityId.Cook:
                FinishCooking(m, task);
                break;
            case ActivityId.Bbq:
                FinishCooking(m, task, "satay");
                break;
            case ActivityId.MakeDrink:
                ServeWarmDrinks(m);
                break;
            case ActivityId.Eat:
                FinishEating(m, task);
                break;
            case ActivityId.Snack:
                if (!State.Inventory.Take("snack") && !State.Inventory.Take("fruit"))
                {
                    m.Needs.Add(NeedKind.Hunger, -15f);
                    Say(m, Loc.T("Kulkasnya kosong...", "The fridge is empty..."));
                }

                break;
            case ActivityId.Read:
                State.AddStat(Stat.BooksRead);
                if (m.Id == MemberId.YoungerSister && Random.Chance(0.3f))
                {
                    Say(m, "Once upon a time... " + Loc.T("Seru sekali bukunya!", "This book is great!"), "ys_book");
                }

                break;
            case ActivityId.Draw:
                if (Random.Chance(m.Id == MemberId.OlderSister ? 0.35f : 0.15f))
                {
                    long price = 15_000 + (long)(m.Skills[SkillKind.Drawing] * 6_000);
                    State.Wallet.Earn(price, Loc.T($"Gambar {m.Name} dibeli tetangga", $"A neighbour bought {m.Name}'s drawing"), Clock.DayIndex, m.IsChild);
                    Bus.Notice(Loc.T($"Gambar {m.Name} dibeli tetangga! +{Loc.Money(price)}", $"A neighbour bought {m.Name}'s drawing! +{Loc.Money(price)}"), "🖼", NoticeKind.Money);
                }

                break;
            case ActivityId.Garden:
                State.Inventory.Add("vegetables", 1);
                if (Random.Chance(0.4f))
                {
                    State.Inventory.Add("fruit", 1);
                }

                Bus.Effect(EffectKind.Leaves, pos, 0.6f);
                break;
            case ActivityId.Craft:
                {
                    long price = 20_000 + (long)(m.Skills[SkillKind.Crafting] * 8_000);
                    State.Wallet.Earn(price, Loc.T("Kerajinan terjual", "Craft sold"), Clock.DayIndex, m.IsChild);
                    Bus.Notice(Loc.T($"Kerajinan {m.Name} terjual: +{Loc.Money(price)}", $"{m.Name}'s craft sold: +{Loc.Money(price)}"), "✂", NoticeKind.Money);
                    break;
                }

            case ActivityId.Repair:
                FinishRepair(m, task, item);
                break;
            case ActivityId.FixPower:
                if (!State.House.PowerOn)
                {
                    bool ok = Random.Chance(0.55f + (m.Skills[SkillKind.Repair] * 0.08f) + (m.Skills[SkillKind.Science] * 0.04f));
                    if (ok)
                    {
                        Scenario?.OnPowerFixed(m);
                    }
                    else
                    {
                        Say(m, Loc.T("Belum bisa... coba lagi.", "Not yet... let me try again."));
                    }
                }

                break;
            case ActivityId.Clean:
                {
                    int removed = State.House.Hazards.RemoveAll(h => h.Kind is HazardKind.Puddle or HazardKind.Flour or HazardKind.BrokenGlass or HazardKind.Debris
                        && Vector2.Distance(h.Position, pos) < 4.5f);
                    if (removed > 0)
                    {
                        Bus.Notice(Loc.T($"{m.Name} membersihkan rumah.", $"{m.Name} cleaned up."), "🧹", NoticeKind.Good);
                        Scenario?.OnCleaned(m, removed);
                    }

                    break;
                }

            case ActivityId.PlayWithPet:
                foreach (Pet pet in State.Pets.Where(p => Vector2.Distance(p.Position, pos) < 5f))
                {
                    pet.Happiness = MathF.Min(100, pet.Happiness + 15);
                    pet.Trust = MathF.Min(100, pet.Trust + 4);
                    Bus.Effect(EffectKind.Hearts, pet.Position, 0.8f);
                }

                break;
            case ActivityId.SellLemonade when !task.FromPlayer:
                {
                    long earned = (long)((20_000 + (m.Skills[SkillKind.Shopping] * 6_000)) * (State.Weather.Current == WeatherKind.Sunny ? 1.4f : 0.7f));
                    State.Wallet.Earn(earned, Loc.T("Jualan es limun", "Lemonade stand"), Clock.DayIndex, true);
                    Bus.Notice(Loc.T($"{m.Name} jualan limun: +{Loc.Money(earned)}", $"{m.Name} sold lemonade: +{Loc.Money(earned)}"), "🍋", NoticeKind.Money);
                    break;
                }

            case ActivityId.FirstAid:
                m.Needs.Add(NeedKind.Health, 20);
                State.Inventory.Take("bandage");
                break;
            case ActivityId.Exercise:
                m.Needs.Add(NeedKind.Health, 3);
                break;
            case ActivityId.CheckCameras:
                Scenario?.OnCamerasChecked(m);
                break;
            case ActivityId.Comfort when task.Partner is { } scared:
                {
                    FamilyMember other = State.Member(scared);
                    other.Mood.Remove("thunder");
                    other.Mood.Remove("scared");
                    other.Mood.Add("comforted", Loc.T("Ditenangkan keluarga", "Comforted by family"), 10, MoodKind.Content, Now, 120);
                    State.Relationships.Change(m.Id, scared, 4f);
                    Bus.Effect(EffectKind.Hearts, other.Position, 1.6f);
                    Scenario?.OnComforted(m, other);
                    break;
                }

            case ActivityId.Swim:
                if (m.IsChild && Random.Chance(0.2f))
                {
                    CreateMemory(Loc.T("Belajar berenang", "Learning to swim"), Loc.T($"{m.Name} berenang di kolam rumah.", $"{m.Name} swam in the home pool."),
                        MemoryKind.Play, EmotionalOutcome.Joyful, [m.Id], RoomId.Pool.ToString(), 2f, "swim");
                }

                break;
        }

        Scenario?.OnTaskCompleted(m, activity, task);
    }

    private void Practice(FamilyMember m, SkillKind skill, float amount)
    {
        if (m.Skills.Practice(skill, amount))
        {
            int level = m.Skills.Level(skill);
            Bus.Publish(new SkillUpEvent(m.Id, skill, level));
            Bus.Notice(Loc.T($"{m.Name}: {Skills.Name(skill)} naik ke level {level}!", $"{m.Name}: {Skills.Name(skill)} reached level {level}!"), Skills.Icon(skill), NoticeKind.Skill);
            Bus.Effect(EffectKind.Stars, m.Position, 2f);
        }
    }

    // ---------------------------------------------------------------- cooking

    /// <summary>The player starts cooking a recipe: walk to the stove, then the mini-game opens.</summary>
    public bool StartPlayerCooking(string recipeId)
    {
        Recipe recipe = Recipes.Get(recipeId);
        FamilyMember m = Controlled;
        if (!recipe.CanMake(State.Inventory))
        {
            Bus.Notice(Loc.T("Bahannya kurang. Beli di supermarket.", "Missing ingredients. Buy them at the supermarket."), "🛒", NoticeKind.Warning);
            return false;
        }

        ActivityId activity = recipe.GrillOnly ? ActivityId.Bbq : ActivityId.Cook;
        if (FindFurniture(m, activity, allowBusy: true) is not { } stove)
        {
            Bus.Notice(Loc.T("Tidak ada kompor yang bisa dipakai.", "No working stove available."), "🍳", NoticeKind.Warning);
            return false;
        }

        StartTask(m, ActivityId.Cook, stove.Item, stove.Slot, fromPlayer: true, recipe: recipeId, minutes: MathF.Min(recipe.Minutes, 20f));
        return true;
    }

    /// <summary>Result of a mini-game run by the presentation layer (0-1 score).</summary>
    public void CompleteMiniGame(float score)
    {
        MiniGameRequest? request = PendingMiniGame;
        PendingMiniGame = null;
        if (request is null)
        {
            return;
        }

        FamilyMember m = Controlled;
        switch (request.Kind)
        {
            case "cook" when m.Task is { Activity: ActivityId.Cook } task:
                task.MiniGameScore = Math.Clamp(score, 0f, 1f);
                task.Remaining = MathF.Min(task.Remaining, 2f);
                break;
            case "lemonade":
                {
                    long earned = (long)(score * 90_000) + 10_000;
                    State.Wallet.Earn(earned, Loc.T("Jualan es limun", "Lemonade stand"), Clock.DayIndex, true);
                    State.Inventory.Take("fruit");
                    State.Inventory.Take("sugar");
                    Practice(m, SkillKind.Shopping, 0.5f + score);
                    Bus.Notice(Loc.T($"Jualan limun laku! +{Loc.Money(earned)}", $"Lemonade sold! +{Loc.Money(earned)}"), "🍋", NoticeKind.Money);
                    Bus.Effect(EffectKind.Coins, m.Position, 1.8f);
                    Bus.Sound("coins");
                    if (m.Task is { Activity: ActivityId.SellLemonade } t)
                    {
                        t.Remaining = 0.1f;
                    }

                    break;
                }

            case "kerupuk":
                {
                    State.AddStat(Stat.FestivalGames);
                    m.Needs.Add(NeedKind.Fun, 25);
                    m.Needs.Add(NeedKind.Hunger, 10);
                    bool won = score >= 0.99f;
                    if (won)
                    {
                        State.Wallet.Earn(50_000, Loc.T("Juara lomba makan kerupuk", "Cracker contest prize"), Clock.DayIndex, true);
                        Bus.Effect(EffectKind.Confetti, m.Position, 2f);
                        Bus.Sound("fanfare");
                    }

                    CreateMemory(won ? Loc.T("Juara makan kerupuk!", "Cracker eating champion!") : Loc.T("Lomba makan kerupuk", "The cracker eating contest"),
                        won ? Loc.T($"{m.Name} menghabiskan kerupuk paling cepat tanpa pakai tangan!", $"{m.Name} finished the cracker first, no hands allowed!")
                            : Loc.T($"{m.Name} tertawa sampai remah kerupuk berjatuhan.", $"{m.Name} laughed so hard the cracker crumbs flew everywhere."),
                        MemoryKind.Play, EmotionalOutcome.Joyful, [m.Id, .. State.Party], WorldMap.Name(PlaceId.Park), won ? 2.5f : 1.5f, $"kerupuk:{Clock.DayIndex}");
                    break;
                }

            case "tug":
                {
                    State.AddStat(Stat.FestivalGames);
                    List<MemberId> team = [m.Id, .. State.Party];
                    bool won = score >= 0.5f;
                    foreach (FamilyMember member in State.Members.Where(x => team.Contains(x.Id)))
                    {
                        member.Needs.Add(NeedKind.Fun, 25);
                        member.Needs.Add(NeedKind.Social, 20);
                        member.Stamina.Value -= 12;
                        foreach (MemberId other in team.Where(o => o != member.Id))
                        {
                            State.Relationships.Change(member.Id, other, 3f);
                        }
                    }

                    if (won)
                    {
                        State.Wallet.Earn(75_000, Loc.T("Juara tarik tambang", "Tug of war prize"), Clock.DayIndex, true);
                        Bus.Effect(EffectKind.Confetti, m.Position, 2.5f);
                        Bus.Sound("fanfare");
                    }

                    CreateMemory(won ? Loc.T("Menang tarik tambang!", "Tug of war winners!") : Loc.T("Tarik tambang bersama", "Tug of war together"),
                        won ? Loc.T("Satu, dua, tarik! Tim keluarga kita menang dan jatuh terduduk sambil tertawa.", "One, two, pull! Our family team won and landed laughing in the grass.")
                            : Loc.T("Kalah tipis, tapi semua tertawa bersama di rumput.", "We lost by a whisker, but everyone laughed together in the grass."),
                        MemoryKind.Play, won ? EmotionalOutcome.Proud : EmotionalOutcome.Joyful, team, WorldMap.Name(PlaceId.Park), team.Count >= 3 ? 3f : 2f, $"tug:{Clock.DayIndex}");
                    break;
                }

            case "arcade":
                {
                    m.Needs.Add(NeedKind.Fun, 30 * score + 10);
                    if (score > 0.7f)
                    {
                        Bus.Notice(Loc.T("Skor tinggi! Dapat hadiah boneka.", "High score! You won a plush toy."), "🏆", NoticeKind.Good);
                        State.Inventory.Add("doll");
                    }

                    break;
                }
        }
    }

    private void FinishCooking(FamilyMember m, MemberTask task, string? forcedRecipe = null)
    {
        string recipeId = forcedRecipe ?? task.RecipeId ?? ChooseRecipeFor(m);
        Recipe recipe = Recipes.Get(recipeId);
        if (!recipe.CanMake(State.Inventory))
        {
            Say(m, Loc.T("Bahannya habis!", "We're out of ingredients!"));
            return;
        }

        foreach ((string item, int count) in recipe.Ingredients)
        {
            State.Inventory.Take(item, count);
        }

        // Cooking together: anyone else working at the stove helps.
        List<MemberId> cooks = [m.Id];
        foreach (FamilyMember other in State.Members.Where(o => o.Id != m.Id && o.Task is { Activity: ActivityId.Cook, Phase: TaskPhase.Performing } && Vector2.Distance(o.Position, m.Position) < 2.5f))
        {
            cooks.Add(other.Id);
            State.Relationships.Change(m.Id, other.Id, 5f);
            Practice(other, SkillKind.Cooking, 0.3f);
        }

        float skill = m.Skills[SkillKind.Cooking];
        CookQuality quality = Recipes.Evaluate(recipe, skill, task.MiniGameScore, cooks.Count > 1, Random);
        Practice(m, SkillKind.Cooking, 0.25f + (recipe.MinSkill * 0.1f));

        State.Pantry.Add(new PreparedFood { RecipeId = recipeId, Quality = quality, Servings = quality == CookQuality.Failed ? 1 : recipe.Servings, CookedDay = Clock.DayIndex, Cooks = cooks });
        if (task.FromPlayer)
        {
            State.AddStat(Stat.PlayerCooked);
        }

        if (recipeId == "pancakes")
        {
            State.AddStat(Stat.PancakesMade);
        }

        string who = string.Join(" & ", cooks.Select(FamilyNames.Short));
        Bus.Notice(Loc.T($"{who} memasak {recipe.Name}: {Recipes.QualityName(quality)} {Recipes.QualityStars(quality)}", $"{who} cooked {recipe.Name}: {Recipes.QualityName(quality)} {Recipes.QualityStars(quality)}"),
            recipe.Icon, quality >= CookQuality.Delicious ? NoticeKind.Good : quality == CookQuality.Failed ? NoticeKind.Warning : NoticeKind.Info);
        Bus.Effect(EffectKind.Steam, m.Position + (new Vector2(MathF.Sin(m.Yaw), MathF.Cos(m.Yaw)) * 0.6f), 1.3f);

        switch (quality)
        {
            case CookQuality.Perfect:
                State.AddStat(Stat.PerfectDishes);
                Bus.Effect(EffectKind.Sparkles, m.Position, 1.5f);
                Bus.Sound("success");
                foreach (MemberId id in cooks)
                {
                    State.Member(id).Mood.Add("perfect-dish", Loc.T("Masakan sempurna!", "A perfect dish!"), 15, MoodKind.Proud, Now, 240);
                }

                CreateMemory(Loc.T($"{recipe.Name} sempurna", $"Perfect {recipe.Name}"),
                    Loc.T($"{who} membuat {recipe.Name} yang sempurna. Semua minta tambah!", $"{who} made a perfect {recipe.Name}. Everyone wanted seconds!"),
                    MemoryKind.Cooking, EmotionalOutcome.Proud, cooks, Rooms.Name(RoomId.Kitchen), 3f, $"recipe:{recipeId}");
                break;
            case CookQuality.Failed:
                // Harmless, funny mistakes at low skill (design section 2 and 9).
                if (recipeId is "pancakes" or "cupcakes" or "birthday-cake" && Random.Chance(0.5f))
                {
                    State.House.AddHazard(HazardKind.Flour, m.Position + new Vector2(0.4f, -0.3f), 0.8f);
                    Bus.Effect(EffectKind.Flour, m.Position, 1.2f, 1.5f);
                    Say(m, Loc.T("Ups! Tepungnya tumpah ke mana-mana!", "Oops! Flour everywhere!"));
                    CreateMemory(Loc.T("Dapur penuh tepung", "Flour everywhere"),
                        Loc.T($"{who} mencoba membuat {recipe.Name}, tapi tepungnya beterbangan ke seluruh dapur!", $"{who} tried to make {recipe.Name} but flour went everywhere!"),
                        MemoryKind.Cooking, EmotionalOutcome.Funny, cooks, Rooms.Name(RoomId.Kitchen), 2f, $"funny:{recipeId}");
                }
                else
                {
                    Bus.Effect(EffectKind.Smoke, m.Position, 1.4f, 1.2f);
                    State.House.AddHazard(HazardKind.Smoke, m.Position, 0.6f);
                    Say(m, Loc.T($"Yah... {recipe.Name}-nya gosong!", $"Oh no... the {recipe.Name} is burnt!"));
                    Bus.Sound("alarm-beep", new Vector3(m.Position.X, 2.4f, m.Position.Y), 0.6f);
                    if (State.House.InRoom(RoomId.Kitchen).Any() && Random.Chance(0.06f * Director.DangerMultiplier(Rarity.Rare)) && Scenario is null)
                    {
                        StartScenario(ScenarioKind.SmallFire);
                    }
                }

                break;
        }

        // A memory when the youngest cooks with Mother: she will want to do it again.
        if (cooks.Contains(MemberId.YoungerSister) && cooks.Contains(MemberId.Mother) && !State.Memories.Any(mem => mem.Tag == $"together:{recipeId}"))
        {
            CreateMemory(Loc.T($"Memasak {recipe.Name} bersama Ibu", $"Making {recipe.Name} with Mom"),
                Loc.T($"Dinda membantu Ibu membuat {recipe.Name}. Dinda ingin membuatnya lagi!", $"Dinda helped Mom make {recipe.Name}. She wants to do it again!"),
                MemoryKind.Cooking, EmotionalOutcome.Heartwarming, cooks, Rooms.Name(RoomId.Kitchen), 2.5f, $"together:{recipeId}");
        }

        // Mother's home bakery sells extra cupcakes and cakes.
        if (m.Id == MemberId.Mother && recipeId is "cupcakes" or "pudding" && quality >= CookQuality.Normal)
        {
            long income = recipe.SellPrice * 4 * ((int)quality);
            State.Wallet.Earn(income, Loc.T("Usaha kue Ibu", "Mom's home bakery"), Clock.DayIndex);
            Bus.Notice(Loc.T($"Kue buatan Ibu laku dijual: +{Loc.Money(income)}", $"Mom's bakery sold treats: +{Loc.Money(income)}"), "🧁", NoticeKind.Money);
            State.Pantry[^1].Servings = Math.Max(2, recipe.Servings - 4);
        }

        Scenario?.OnCooked(m, recipe, quality);
        if (!recipe.IsDrink && Hour is >= 6.2f and < 8.5f or >= 11.5f and < 13.5f or >= 17.4f and < 20f)
        {
            CallFamilyToTable(m);
        }
    }

    /// <summary>"Makan bersama!": everyone at home who is free comes to the table at once.</summary>
    public void CallFamilyToTable(FamilyMember? caller = null)
    {
        if (State.MealServings <= 0 || Scenario is { Major: true })
        {
            return;
        }

        int seated = 0;
        foreach (FamilyMember m in State.Members.Where(m => !m.Away && !m.InDanger && m.Id != State.Controlled
            && Rooms.Lot.Contains(m.Position) && m.Task?.Activity is not (ActivityId.Eat or ActivityId.Sleep or ActivityId.Shower)
            && m.Needs[NeedKind.Hunger] < 92f))
        {
            PreparedFood? food = State.Pantry.Where(p => !p.Recipe.IsDrink && p.Servings > 0).OrderByDescending(p => p.Quality).FirstOrDefault();
            if (food is null || FindFurniture(m, ActivityId.Eat) is not { } seat)
            {
                break;
            }

            food.Servings--;
            State.Pantry.RemoveAll(p => p.Servings <= 0);
            StartTask(m, ActivityId.Eat, seat.Item, seat.Slot, tag: $"quality:{food.Quality}", recipe: food.RecipeId);
            seated++;
        }

        if (seated >= 2 && caller is not null)
        {
            Say(caller, Hour < 10 ? Loc.T("Sarapan sudah siap! Ayo makan bersama!", "Breakfast is ready! Let's eat together!") : Loc.T("Makanan siap! Ayo makan bersama!", "Food's ready! Let's eat together!"),
                caller.Id == MemberId.Mother ? (Hour < 10 ? "mom_breakfast" : "mom_dinner") : null);
        }
    }

    private string ChooseRecipeFor(FamilyMember m)
    {
        // Sunday pancake morning, birthdays, sick family members, or what is possible.
        bool morning = Hour < 10f;
        if (morning && Date.Weekday == Time.Weekday.Sunday && Recipes.Get("pancakes").CanMake(State.Inventory))
        {
            return "pancakes";
        }

        if (Time.Calendar.EventsOn(Date).Any(e => e.Kind == Time.CalendarEventKind.Birthday) && !State.Pantry.Any(p => p.RecipeId == "birthday-cake")
            && m.Skills[SkillKind.Cooking] >= 3 && Recipes.Get("birthday-cake").CanMake(State.Inventory))
        {
            return "birthday-cake";
        }

        if (State.Members.Any(o => o.Sick) && Recipes.Get("soup").CanMake(State.Inventory))
        {
            return "soup";
        }

        // Memories steer choices: a dish someone loved making is suggested again.
        if (State.Memories.LastOrDefault(mem => mem.Tag.StartsWith("together:", StringComparison.Ordinal) && mem.Involves(m.Id)) is { } memory
            && Recipes.Exists(memory.Tag["together:".Length..]) && Random.Chance(0.4f) && Recipes.Get(memory.Tag["together:".Length..]).CanMake(State.Inventory))
        {
            return memory.Tag["together:".Length..];
        }

        string[] options = morning
            ? ["fried-rice", "eggs", "pancakes", "bubur-ayam", "mie-goreng"]
            : ["fried-rice", "chicken", "soup", "eggs", "salad", "mie-goreng", "gado-gado"];
        List<string> possible = [.. options.Where(o => Recipes.Get(o).CanMake(State.Inventory) && Recipes.Get(o).MinSkill <= m.Skills[SkillKind.Cooking] + 1.5f)];
        if (m.Id == MemberId.Mother && Random.Chance(0.25f) && Recipes.Get("cupcakes").CanMake(State.Inventory))
        {
            return "cupcakes";
        }

        if (possible.Count > 0)
        {
            return Random.Pick(possible);
        }

        // Anything everyday that the kitchen can still make (never a dish that will fail).
        return Recipes.All.Where(r => !r.IsDrink && !r.GrillOnly && !r.Celebration && r.CanMake(State.Inventory))
            .OrderBy(r => r.MinSkill).Select(r => r.Id).FirstOrDefault() ?? "eggs";
    }

    private void ServeWarmDrinks(FamilyMember maker)
    {
        if (!State.Inventory.Take("milk"))
        {
            Say(maker, Loc.T("Susunya habis...", "We're out of milk..."));
            return;
        }

        State.Inventory.Take("chocolate");
        foreach (FamilyMember m in State.Members.Where(o => !o.Away && Vector2.Distance(o.Position, maker.Position) < 9f))
        {
            m.Mood.Add("warm-drink", Loc.T("Cokelat hangat", "Hot chocolate"), 8, MoodKind.Content, Now, 120);
            m.Needs.Add(NeedKind.Social, 6);
            m.Mood.Remove("thunder");
        }

        Bus.Notice(Loc.T($"{maker.Name} membuat cokelat hangat untuk semua.", $"{maker.Name} made hot chocolate for everyone."), "☕", NoticeKind.Good);
        Bus.Effect(EffectKind.Steam, maker.Position, 1.3f);
        Scenario?.OnWarmDrinks(maker);
    }

    private void FinishEating(FamilyMember m, MemberTask task)
    {
        // Count people eating together right now: a family meal.
        int together = State.Members.Count(o => o.Id != m.Id && (o.Task is { Activity: ActivityId.Eat } || o.Mood.Has("ate-just-now"))) + 1;
        m.Mood.Add("ate-just-now", "", 0, MoodKind.Content, Now, 20);
        if (together >= 3)
        {
            m.Mood.Add("family-meal", Loc.T("Makan bersama keluarga", "Ate with the family"), 10, MoodKind.Happy, Now, 240);
            foreach (FamilyMember o in State.Members.Where(o => o.Id != m.Id && o.Task is { Activity: ActivityId.Eat }))
            {
                State.Relationships.Change(m.Id, o.Id, 0.8f);
            }

            if (!State.Flag($"meal:{Clock.DayIndex}:{(Hour < 11 ? "b" : Hour < 15 ? "l" : "d")}"))
            {
                State.Flags.Add($"meal:{Clock.DayIndex}:{(Hour < 11 ? "b" : Hour < 15 ? "l" : "d")}");
                State.AddStat(Stat.FamilyMeals);
                if (State.Stat(Stat.FamilyMeals) == 1)
                {
                    CreateMemory(Loc.T("Sarapan pertama di rumah baru", "First breakfast in the new home"),
                        Loc.T("Seluruh keluarga makan bersama di meja makan rumah baru kita.", "The whole family ate together at the new dining table."),
                        MemoryKind.Meal, EmotionalOutcome.Heartwarming, [.. State.Members.Where(o => !o.Away).Select(o => o.Id)], Rooms.Name(RoomId.Kitchen), 3f, "first-meal");
                }
            }
        }

        if (task.Tag.Contains("Failed", StringComparison.Ordinal))
        {
            m.Mood.Add("bad-food", Loc.T("Makanannya gosong", "Burnt food"), -6, MoodKind.Annoyed, Now, 90);
        }
        else if (task.Tag.Contains("Perfect", StringComparison.Ordinal) || task.Tag.Contains("Delicious", StringComparison.Ordinal))
        {
            m.Mood.Add("tasty", Loc.T("Makanan lezat!", "Delicious food!"), 8, MoodKind.Happy, Now, 180);
        }

        if (m.Sick && task.RecipeId == "soup")
        {
            m.Needs.Add(NeedKind.Health, 25);
        }
    }

    // ---------------------------------------------------------------- repairs

    private void FinishRepair(FamilyMember m, MemberTask task, FurnitureItem? item)
    {
        if (item is null || !item.Broken)
        {
            return;
        }

        float skill = m.Skills[SkillKind.Repair];
        bool electronic = item.Def.NeedsPower;
        if (electronic)
        {
            // Science knowledge helps with household electronics (design section 10).
            skill += m.Skills[SkillKind.Science] * 0.4f;
        }

        float chance = Math.Clamp(0.35f + (skill * 0.12f), 0.3f, 0.97f);
        if (Random.Chance(chance))
        {
            item.Broken = false;
            item.Condition = 100f;
            State.AddStat(Stat.Repairs);
            Practice(m, SkillKind.Repair, 0.6f);
            Bus.Notice(Loc.T($"{m.Name} berhasil memperbaiki {item.Def.Name}!", $"{m.Name} fixed the {item.Def.Name}!"), "🔧", NoticeKind.Good);
            Bus.Effect(EffectKind.Sparkles, item.Position, 1.0f);
            Bus.Sound("success");
            m.Mood.Add("fixed", Loc.T("Berhasil memperbaiki", "Fixed something"), 8, MoodKind.Proud, Now, 180);
            if (m.Id == MemberId.Father)
            {
                Say(m, Loc.T("Beres! Seperti baru!", "Done! Good as new!"), "dad_fix");
            }

            Scenario?.OnRepaired(m, item);
            State.House.TouchFurniture();
        }
        else
        {
            Practice(m, SkillKind.Repair, 0.3f);
            if (item.DefId is "sink" or "shower" or "washer" && Random.Chance(0.6f))
            {
                // The classic: a failed repair sprays water through the room.
                State.House.AddHazard(HazardKind.Puddle, item.Position + new Vector2(0.3f, 0.6f), 0.6f);
                Bus.Effect(EffectKind.Splash, item.Position, 1.0f, 2f);
                Bus.Sound("splash");
                Say(m, Loc.T("Waaah! Airnya muncrat!", "Waaah! Water everywhere!"));
                CreateMemory(Loc.T("Keran yang nakal", "The naughty faucet"),
                    Loc.T($"{m.Name} mencoba memperbaiki keran, tapi air malah menyembur ke seluruh ruangan!", $"{m.Name} tried to fix the faucet and it sprayed water all over the room!"),
                    MemoryKind.Play, EmotionalOutcome.Funny, [m.Id], Rooms.Name(item.Room), 2f, "funny-faucet");
            }
            else
            {
                Say(m, Loc.T("Hmm, belum berhasil. Coba lagi nanti.", "Hmm, not fixed yet. I'll try again."));
            }
        }
    }
}

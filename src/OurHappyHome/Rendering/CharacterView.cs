using System.Numerics;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Scenarios;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;
using ThreeNet;
using AColor = Avalonia.Media.Color;

namespace OurHappyHome.Rendering;

/// <summary>
/// The five rigged family members (Rodin models rigged and animated in
/// Blender), with animation cross-fades, sitting / lying anchors, speech
/// bubbles, danger markers and the controlled member's ring and flashlight.
/// Also draws pets, neighbours and scenario visitors.
/// </summary>
public sealed class CharacterView
{
    private sealed class Rig
    {
        public MemberId Id;
        public Node Root = null!;
        public Node Model = null!;
        public Dictionary<string, AnimationClip> Clips = [];
        public AnimationPlayer? Current;
        public AnimationPlayer? Previous;
        public string CurrentName = "";
        public float Fade = 1f;
        public float Height;
        public float HipHeight;
        public Node? Bubble;
        public string? BubbleText;
        public Node? Marker;
        public string? MarkerGlyph;
        public Node? Tag;
        public Mannequin? Fallback;
    }

    private readonly Scene _scene;
    private readonly Meshes _m;
    private readonly Textures _t;
    private readonly ModelLibrary _models;
    private readonly Node _root;
    private readonly Dictionary<MemberId, Rig> _rigs = [];
    private readonly Dictionary<int, (Node Node, Node Model, PetKind Kind)> _pets = [];
    private readonly Dictionary<string, Mannequin> _npcs = [];
    private readonly Dictionary<(Scenario, int), Node> _actors = [];
    private readonly Node _ring;
    private readonly Node _flashlight;
    private float _time;

    /// <summary>Hip height relative to body height, measured when rigging.</summary>
    private static readonly Dictionary<MemberId, (float Height, float Hip)> Proportions = new()
    {
        [MemberId.Father] = (1.74f, 0.71f),
        [MemberId.Mother] = (1.64f, 0.80f),
        [MemberId.OlderSister] = (1.42f, 0.70f),
        [MemberId.Player] = (1.36f, 0.67f),
        [MemberId.YoungerSister] = (1.22f, 0.60f),
    };

    public CharacterView(Scene scene, Meshes meshes, Textures textures, ModelLibrary models)
    {
        _scene = scene;
        _m = meshes;
        _t = textures;
        _models = models;
        _root = scene.CreateNode(null, "characters");

        foreach (MemberId id in FamilyNames.All)
        {
            _rigs[id] = LoadRig(id);
        }

        _ring = _scene.AddMesh(_m.Torus, _t.Solid("#7CFF9B", 0.4f, emissive: 1.5f), _root, "player-ring");
        _ring.Scale = new Vector3(0.45f, 0.6f, 0.45f);
        _ring.CastShadow = false;

        _flashlight = _scene.AddLight(Light.Spot(new Vector3(1f, 0.96f, 0.85f), 30f, 16f, 0.28f, 0.55f) with { Enabled = false }, _root, "flashlight");
    }

    private Rig LoadRig(MemberId id)
    {
        (float height, float hip) = Proportions[id];
        Rig rig = new() { Id = id, Height = height, HipHeight = hip };
        rig.Root = _scene.CreateNode(_root, $"member-{id}");
        rig.Model = rig.Root.CreateChild("model");
        string path = ModelLibrary.ModelPath(FamilyNames.ModelName(id));
        if (File.Exists(path))
        {
            try
            {
                int before = _scene.Animations.Count;
                ImportResult import = _scene.LoadGltf(path, rig.Model);
                foreach (AnimationClip clip in _scene.Animations.Skip(before))
                {
                    rig.Clips[clip.Name] = clip;
                }

                rig.Model.SetShadowsRecursive(true, true);
                if (import.AnimationCount == 0)
                {
                    rig.Clips.Clear();
                }
            }
            catch (ThreeNetException)
            {
                rig.Fallback = new Mannequin(_scene, _m, _t, rig.Model, FamilyNames.Accent(id), height);
            }
        }
        else
        {
            rig.Fallback = new Mannequin(_scene, _m, _t, rig.Model, FamilyNames.Accent(id), height);
        }

        Play(rig, "Idle");
        return rig;
    }

    private static void Play(Rig rig, string name)
    {
        if (rig.CurrentName == name)
        {
            return;
        }

        if (!rig.Clips.TryGetValue(name, out AnimationClip? clip) && !rig.Clips.TryGetValue("Idle", out clip))
        {
            rig.CurrentName = name;
            return;
        }

        rig.Previous?.Stop();
        rig.Previous = rig.Current;
        rig.Current = clip.Play(loop: true, speed: 1f, weight: rig.Previous is null ? 1f : 0f);
        rig.CurrentName = name;
        rig.Fade = rig.Previous is null ? 1f : 0f;
    }

    public void Update(GameSession session, float dt, Quaternion cameraRotation)
    {
        _time += dt;
        foreach (FamilyMember m in session.State.Members)
        {
            UpdateMember(session, m, _rigs[m.Id], dt, cameraRotation);
        }

        FamilyMember controlled = session.Controlled;
        Rig me = _rigs[controlled.Id];
        _ring.Visible = !controlled.Away;
        _ring.Position = new Vector3(controlled.Position.X, (controlled.Anchor?.Y ?? 0f) + 0.04f, controlled.Position.Y);
        _ring.EulerAngles = new Vector3(0f, _time * 1.5f, 0f);

        bool flashlight = session.FlashlightOn;
        if (_flashlight.Light is { } light && light.Enabled != flashlight)
        {
            _flashlight.Light = light with { Enabled = flashlight };
        }

        if (flashlight)
        {
            Vector2 forward = new(MathF.Sin(controlled.Yaw), MathF.Cos(controlled.Yaw));
            Vector3 from = new(controlled.Position.X + (forward.X * 0.3f), 1.15f, controlled.Position.Y + (forward.Y * 0.3f));
            _flashlight.Position = from;
            _flashlight.LookAt(from + new Vector3(forward.X * 4f, -1.2f, forward.Y * 4f));
        }

        _ = me;
        UpdatePets(session, dt);
        UpdateNpcs(session, dt, cameraRotation);
        UpdateActors(session, dt);
    }

    private void UpdateMember(GameSession session, FamilyMember m, Rig rig, float dt, Quaternion cameraRotation)
    {
        rig.Root.Visible = !m.Away;
        if (m.Away)
        {
            return;
        }

        // Cross-fade between clips.
        Play(rig, m.Animation);
        if (rig.Previous is not null)
        {
            rig.Fade = MathF.Min(1f, rig.Fade + (dt * 5f));
            if (rig.Current is not null)
            {
                rig.Current.Weight = rig.Fade;
            }

            rig.Previous.Weight = 1f - rig.Fade;
            if (rig.Fade >= 1f)
            {
                rig.Previous.Stop();
                rig.Previous = null;
            }
        }

        if (rig.Current is not null)
        {
            float speed = m.Moving ? (m.Running ? 1.15f : 1f) * (session.SpeedModifier(m)) : 1f;
            rig.Current.Speed = speed * (session.EffectiveSpeed > 4f ? 2f : 1f);
        }

        rig.Fallback?.Animate(m.Moving, m.Running, _time);

        Vector3 position = new(m.Position.X, 0f, m.Position.Y);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, m.Yaw);
        Vector3 modelOffset = Vector3.Zero;
        Quaternion modelRotation = Quaternion.Identity;

        if (m.Anchor is { } anchor)
        {
            switch (m.Pose)
            {
                case AnchorPose.Sit:
                    position = anchor;
                    modelOffset = new Vector3(0f, 0.08f - rig.HipHeight + 0.02f, -0.05f);
                    break;
                case AnchorPose.Lie:
                    // Head towards the bed's headboard, face up.
                    position = anchor;
                    modelRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2f);
                    modelOffset = new Vector3(0f, 0.12f, rig.Height * 0.48f);
                    break;
                default:
                    position = anchor;
                    break;
            }
        }
        else if (m.Safety == SafetyState.Down)
        {
            modelOffset = new Vector3(0f, -rig.HipHeight + 0.15f, 0f);
        }

        // Swimming in the pool: sink to the chest.
        if (m.Task?.Activity == ActivityId.Swim && m.Task.Phase == TaskPhase.Performing)
        {
            modelOffset = new Vector3(0f, -rig.Height * 0.55f, 0f);
        }

        rig.Root.SetTransform(position, rotation, Vector3.One);
        rig.Model.SetTransform(Vector3.Transform(modelOffset, Quaternion.Identity), modelRotation, Vector3.One);

        // Speech bubble.
        string? text = m.Bubble is not null && session.Now <= m.BubbleUntil ? m.Bubble : null;
        if (text != rig.BubbleText)
        {
            rig.Bubble?.Remove();
            rig.Bubble = null;
            rig.BubbleText = text;
            if (text is not null)
            {
                (Texture tex, Vector2 size) label = _t.Label(text, AColor.FromArgb(240, 255, 255, 255), AColor.FromArgb(255, 40, 40, 50), 26, 380, bubble: true);
                rig.Bubble = _scene.AddMesh(_m.UnitPlane, _t.LabelMaterial(label), _root, "bubble");
                rig.Bubble.CastShadow = false;
                rig.Bubble.Scale = new Vector3(label.size.X / 150f, label.size.Y / 150f, 1f);
            }
        }

        if (rig.Bubble is { } bubble)
        {
            Vector3 s = bubble.Scale;
            bubble.SetTransform(position + new Vector3(0f, rig.Height + 0.55f + (s.Y * 0.5f), 0f), cameraRotation, s);
        }

        // Danger marker / sleeping marker.
        string? marker = m.InDanger ? "🆘" : m.Task is { Activity: ActivityId.Sleep, Phase: TaskPhase.Performing } ? "💤" : m.Sick ? "🤒" : null;
        if (marker is null)
        {
            if (rig.Marker is not null)
            {
                rig.Marker.Remove();
                rig.Marker = null;
                rig.MarkerGlyph = null;
            }
        }
        else
        {
            if (rig.Marker is null || rig.MarkerGlyph != marker)
            {
                rig.Marker?.Remove();
                rig.Marker = _scene.AddMesh(_m.UnitPlane, _t.Emoji(marker), _root, "marker");
                rig.Marker.CastShadow = false;
                rig.MarkerGlyph = marker;
            }

            float pulse = m.InDanger ? 0.55f + (0.12f * MathF.Sin(_time * 8f)) : 0.4f;
            float y = m.Pose == AnchorPose.Lie ? position.Y + 0.7f : rig.Height + 0.35f;
            rig.Marker.SetTransform(new Vector3(position.X, y, position.Z), cameraRotation, new Vector3(pulse));
        }

        // Name tag for the others (not the player).
        bool showTag = m.Id != session.State.Controlled && text is null;
        if (showTag)
        {
            if (rig.Tag is null)
            {
                (Texture tex, Vector2 size) label = _t.Label(m.Name, AColor.Parse(FamilyNames.Accent(m.Id)), AColor.FromArgb(255, 255, 255, 255), 24);
                rig.Tag = _scene.AddMesh(_m.UnitPlane, _t.LabelMaterial(label), _root, "tag");
                rig.Tag.CastShadow = false;
                rig.Tag.Scale = new Vector3(label.size.X / 170f, label.size.Y / 170f, 1f);
            }

            Vector3 s = rig.Tag.Scale;
            float tagY = m.Pose == AnchorPose.Lie ? position.Y + 0.45f : position.Y + rig.Height + 0.18f + (m.Pose == AnchorPose.Sit ? -0.4f : 0f);
            rig.Tag.Visible = true;
            rig.Tag.SetTransform(new Vector3(position.X, tagY, position.Z), cameraRotation, s);
        }
        else if (rig.Tag is not null)
        {
            rig.Tag.Visible = false;
        }
    }

    /// <summary>World position of a member's head, for UI anchoring.</summary>
    public Vector3 HeadPosition(FamilyMember m) => new(m.Position.X, (m.Anchor?.Y ?? 0f) + _rigs[m.Id].Height, m.Position.Y);

    // ------------------------------------------------------------------ pets

    private void UpdatePets(GameSession session, float dt)
    {
        foreach (Pet pet in session.State.Pets)
        {
            if (!_pets.TryGetValue(pet.Id, out (Node Node, Node Model, PetKind Kind) visual))
            {
                Node node = _scene.CreateNode(_root, $"pet-{pet.Id}");
                Node model = node.CreateChild("pet-model");
                string modelName = pet.Kind == PetKind.Cat ? "cat" : "dog";
                float height = pet.Kind switch { PetKind.Dog => 0.62f, PetKind.Cat => 0.42f, _ => 0.3f };
                if (pet.Kind is PetKind.Dog or PetKind.Cat && _models.Height(modelName, model, height) is not null)
                {
                    model.SetShadowsRecursive(true, true);
                }
                else
                {
                    _m.Sphere(model, new Vector3(0f, height * 0.5f, 0f), new Vector3(height * 0.9f, height, height * 1.2f), _t.Solid(pet.Kind == PetKind.Rabbit ? "#F2F2F2" : "#D9A066"));
                }

                visual = (node, model, pet.Kind);
                _pets[pet.Id] = visual;
            }

            float bob = pet.Moving ? MathF.Abs(MathF.Sin(_time * 14f)) * 0.06f : 0f;
            float squash = pet.State == PetState.Sleep ? 0.7f : 1f + (0.02f * MathF.Sin(_time * 3f));
            float hop = pet.State == PetState.Bark ? MathF.Abs(MathF.Sin(_time * 12f)) * 0.15f : 0f;
            visual.Node.SetTransform(new Vector3(pet.Position.X, bob + hop, pet.Position.Y), Quaternion.CreateFromAxisAngle(Vector3.UnitY, pet.Yaw), Vector3.One);
            visual.Model.Scale = new Vector3(1f, squash, 1f);
            visual.Model.EulerAngles = new Vector3(0f, 0f, pet.Moving ? MathF.Sin(_time * 14f) * 0.06f : 0f);
        }

        foreach (int id in _pets.Keys.Where(id => session.State.Pets.All(p => p.Id != id)).ToList())
        {
            _pets[id].Node.Remove();
            _pets.Remove(id);
        }
    }

    // ------------------------------------------------------------ neighbours

    private void UpdateNpcs(GameSession session, float dt, Quaternion cameraRotation)
    {
        foreach (Npc npc in session.Npcs)
        {
            if (!_npcs.TryGetValue(npc.Id, out Mannequin? body))
            {
                Node node = _scene.CreateNode(_root, $"npc-{npc.Id}");
                body = new Mannequin(_scene, _m, _t, node, npc.Color, npc.Height, npc.Id switch
                {
                    "grandma" => MannequinStyle.Grandma,
                    "teacher" => MannequinStyle.Teacher,
                    "budi" => MannequinStyle.Hat,
                    _ => MannequinStyle.Kid,
                });
                _npcs[npc.Id] = body;
            }

            bool present = npc.Present(session.Hour);
            body.Root.Visible = present;
            if (present)
            {
                body.Root.SetTransform(new Vector3(npc.Position.X, 0f, npc.Position.Y), Quaternion.CreateFromAxisAngle(Vector3.UnitY, npc.Yaw), Vector3.One);
                body.Animate(npc.Moving, false, _time);
            }
        }
    }

    // ---------------------------------------------------------------- actors

    private void UpdateActors(GameSession session, float dt)
    {
        Scenario? scenario = session.Scenario;
        HashSet<(Scenario, int)> alive = [];
        if (scenario is not null)
        {
            foreach (ScenarioActor actor in scenario.Actors)
            {
                (Scenario, int) key = (scenario, actor.Id);
                alive.Add(key);
                if (!_actors.TryGetValue(key, out Node? node))
                {
                    node = CreateActor(actor.Kind);
                    _actors[key] = node;
                }

                node.Visible = actor.Visible;
                float bob = actor.Moving ? MathF.Abs(MathF.Sin(_time * 12f)) * 0.05f : 0f;
                node.SetTransform(new Vector3(actor.Position.X, bob, actor.Position.Y), Quaternion.CreateFromAxisAngle(Vector3.UnitY, actor.Yaw), Vector3.One);
                if (node.Tag == 1 && node.Children.Count > 0)
                {
                    // Snake: slither the body segments.
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        Node seg = node.Children[i];
                        Vector3 p = seg.Position;
                        seg.Position = new Vector3(MathF.Sin((_time * 4f) + (i * 0.7f)) * 0.12f, p.Y, p.Z);
                    }
                }
            }
        }

        foreach ((Scenario, int) key in _actors.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            _actors[key].Remove();
            _actors.Remove(key);
        }
    }

    private Node CreateActor(ActorKind kind)
    {
        Node node = _scene.CreateNode(_root, $"actor-{kind}");
        switch (kind)
        {
            case ActorKind.Cat:
                if (_models.Height("cat", node, 0.42f) is null)
                {
                    _m.Sphere(node, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.3f, 0.5f), _t.Solid("#E8913A"));
                }

                break;
            case ActorKind.Monkey:
                {
                    Material fur = _t.Solid("#8B5A2B", 0.9f);
                    Material face = _t.Solid("#E8C39E", 0.8f);
                    _m.Sphere(node, new Vector3(0f, 0.35f, 0f), new Vector3(0.38f, 0.45f, 0.32f), fur);
                    _m.Sphere(node, new Vector3(0f, 0.7f, 0.02f), new Vector3(0.3f), fur);
                    _m.Sphere(node, new Vector3(0f, 0.68f, 0.13f), new Vector3(0.2f, 0.16f, 0.1f), face);
                    _m.Sphere(node, new Vector3(-0.16f, 0.74f, 0f), new Vector3(0.1f), face);
                    _m.Sphere(node, new Vector3(0.16f, 0.74f, 0f), new Vector3(0.1f), face);
                    Node tail = _m.Cylinder(node, new Vector3(0f, 0.4f, -0.3f), 0.03f, 0.6f, fur, true);
                    tail.EulerAngles = new Vector3(-0.8f, 0f, 0f);
                    _m.Sphere(node, new Vector3(0.12f, 0.55f, 0.2f), new Vector3(0.16f, 0.1f, 0.1f), _t.Solid("#F7D547"));
                    break;
                }

            case ActorKind.Snake:
                {
                    node.Tag = 1;
                    Material scale = _t.Solid("#5A8F29", 0.5f);
                    for (int i = 0; i < 9; i++)
                    {
                        _m.Sphere(node, new Vector3(0f, 0.08f, -i * 0.16f), new Vector3(0.16f - (i * 0.008f), 0.14f, 0.2f), scale, true);
                    }

                    _m.Sphere(node, new Vector3(0f, 0.1f, 0.12f), new Vector3(0.18f, 0.13f, 0.22f), _t.Solid("#4C7A22", 0.5f));
                    break;
                }

            case ActorKind.Boar:
                {
                    Material fur = _t.Solid("#5B4636", 0.95f);
                    _m.Sphere(node, new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.65f, 1.2f), fur);
                    _m.Sphere(node, new Vector3(0f, 0.55f, 0.6f), new Vector3(0.45f, 0.42f, 0.5f), fur);
                    _m.Cylinder(node, new Vector3(0f, 0.5f, 0.88f), 0.1f, 0.12f, _t.Solid("#C28F7B"));
                    _m.Box(node, new Vector3(-0.15f, 0.45f, 0.82f), new Vector3(0.04f, 0.04f, 0.18f), _t.Solid("#F5F0E1")).EulerAngles = new Vector3(0.6f, 0f, 0f);
                    _m.Box(node, new Vector3(0.15f, 0.45f, 0.82f), new Vector3(0.04f, 0.04f, 0.18f), _t.Solid("#F5F0E1")).EulerAngles = new Vector3(0.6f, 0f, 0f);
                    foreach ((float x, float z) in new[] { (-0.2f, 0.35f), (0.2f, 0.35f), (-0.2f, -0.35f), (0.2f, -0.35f) })
                    {
                        _m.Block(node, x, z, 0f, new Vector3(0.12f, 0.3f, 0.12f), fur);
                    }

                    break;
                }

            default:
                {
                    (string color, MannequinStyle style) = kind switch
                    {
                        ActorKind.Police => ("#22315C", MannequinStyle.Police),
                        ActorKind.Firefighter => ("#C0392B", MannequinStyle.Helmet),
                        ActorKind.Rescuer => ("#8C7A4F", MannequinStyle.Hat),
                        _ => ("#2F2F35", MannequinStyle.Hood),
                    };
                    _ = new Mannequin(_scene, _m, _t, node, color, 1.75f, style);
                    break;
                }
        }

        node.SetShadowsRecursive(true, true);
        return node;
    }
}

public enum MannequinStyle
{
    Plain,
    Kid,
    Grandma,
    Teacher,
    Hat,
    Hood,
    Police,
    Helmet,
}

/// <summary>A friendly low-poly figure used for townsfolk and visitors (and as a fallback rig).</summary>
public sealed class Mannequin
{
    private readonly Node _legL;
    private readonly Node _legR;
    private readonly Node _armL;
    private readonly Node _armR;

    public Mannequin(Scene scene, Meshes m, Textures t, Node root, string color, float height, MannequinStyle style = MannequinStyle.Plain)
    {
        Root = root;
        float s = height / 1.7f;
        Material shirt = t.Solid(color, 0.7f);
        Material skin = t.Solid(style == MannequinStyle.Hood ? "#C9A27E" : "#E8B894", 0.6f);
        Material trousers = t.Solid(style is MannequinStyle.Grandma ? "#6C4F82" : "#2E3A4D", 0.8f);
        Material hair = t.Solid(style == MannequinStyle.Grandma ? "#D7D7D7" : "#3B2A20", 0.8f);

        _legL = root.CreateChild("leg-l");
        _legL.Position = new Vector3(-0.1f * s, 0.82f * s, 0f);
        m.Cylinder(_legL, new Vector3(0f, -0.4f * s, 0f), 0.075f * s, 0.8f * s, trousers, true);
        _legR = root.CreateChild("leg-r");
        _legR.Position = new Vector3(0.1f * s, 0.82f * s, 0f);
        m.Cylinder(_legR, new Vector3(0f, -0.4f * s, 0f), 0.075f * s, 0.8f * s, trousers, true);

        if (style == MannequinStyle.Grandma)
        {
            m.Cone(root, new Vector3(0f, 0.15f * s, 0f), 0.3f * s, 0.85f * s, trousers);
        }

        m.Cylinder(root, new Vector3(0f, 1.12f * s, 0f), 0.2f * s, 0.62f * s, shirt);
        m.Sphere(root, new Vector3(0f, 1.55f * s, 0f), new Vector3(0.3f * s), skin);
        m.Sphere(root, new Vector3(-0.06f * s, 1.58f * s, 0.14f * s), new Vector3(0.04f * s), t.Solid("#1E1E1E"));
        m.Sphere(root, new Vector3(0.06f * s, 1.58f * s, 0.14f * s), new Vector3(0.04f * s), t.Solid("#1E1E1E"));

        _armL = root.CreateChild("arm-l");
        _armL.Position = new Vector3(-0.26f * s, 1.38f * s, 0f);
        m.Cylinder(_armL, new Vector3(0f, -0.3f * s, 0f), 0.055f * s, 0.6f * s, shirt, true);
        _armR = root.CreateChild("arm-r");
        _armR.Position = new Vector3(0.26f * s, 1.38f * s, 0f);
        m.Cylinder(_armR, new Vector3(0f, -0.3f * s, 0f), 0.055f * s, 0.6f * s, shirt, true);

        switch (style)
        {
            case MannequinStyle.Grandma:
                m.Sphere(root, new Vector3(0f, 1.66f * s, -0.08f * s), new Vector3(0.32f * s, 0.24f * s, 0.32f * s), hair);
                m.Sphere(root, new Vector3(0f, 1.78f * s, -0.15f * s), new Vector3(0.14f * s), hair);
                m.Box(root, new Vector3(0f, 1.58f * s, 0.15f * s), new Vector3(0.22f * s, 0.05f * s, 0.02f * s), t.Solid("#B0B6BE", 0.2f, 0.7f));
                break;
            case MannequinStyle.Teacher:
                m.Sphere(root, new Vector3(0f, 1.62f * s, -0.04f * s), new Vector3(0.33f * s, 0.3f * s, 0.32f * s), hair);
                m.Cone(root, new Vector3(0f, 0.2f * s, 0f), 0.28f * s, 0.75f * s, t.Solid("#3D405B"));
                break;
            case MannequinStyle.Hat:
                m.Cylinder(root, new Vector3(0f, 1.72f * s, 0f), 0.2f * s, 0.12f * s, t.Solid("#8E5A3C"));
                m.Cylinder(root, new Vector3(0f, 1.67f * s, 0f), 0.3f * s, 0.03f * s, t.Solid("#8E5A3C"));
                break;
            case MannequinStyle.Hood:
                m.Sphere(root, new Vector3(0f, 1.6f * s, -0.03f * s), new Vector3(0.36f * s, 0.36f * s, 0.36f * s), shirt);
                break;
            case MannequinStyle.Police:
                m.Cylinder(root, new Vector3(0f, 1.72f * s, 0f), 0.17f * s, 0.12f * s, t.Solid("#14213D"));
                m.Box(root, new Vector3(0f, 1.69f * s, 0.15f * s), new Vector3(0.24f * s, 0.03f * s, 0.12f * s), t.Solid("#14213D"));
                m.Box(root, new Vector3(0.08f * s, 1.25f * s, 0.2f * s), new Vector3(0.08f * s, 0.08f * s, 0.02f * s), t.Solid("#FCA311", 0.3f, 0.8f));
                break;
            case MannequinStyle.Helmet:
                m.Sphere(root, new Vector3(0f, 1.68f * s, 0f), new Vector3(0.36f * s, 0.24f * s, 0.4f * s), t.Solid("#F1C40F", 0.4f));
                m.Box(root, new Vector3(0f, 1.1f * s, 0.2f * s), new Vector3(0.36f * s, 0.05f * s, 0.02f * s), t.Solid("#F1F1F1", emissive: 0.6f));
                break;
            case MannequinStyle.Kid:
                m.Sphere(root, new Vector3(0f, 1.66f * s, -0.03f * s), new Vector3(0.32f * s, 0.2f * s, 0.32f * s), hair);
                break;
            default:
                m.Sphere(root, new Vector3(0f, 1.66f * s, -0.03f * s), new Vector3(0.32f * s, 0.2f * s, 0.32f * s), hair);
                break;
        }

        root.SetShadowsRecursive(true, true);
    }

    public Node Root { get; }

    public void Animate(bool moving, bool running, float time)
    {
        float swing = moving ? MathF.Sin(time * (running ? 12f : 7f)) * (running ? 0.8f : 0.45f) : MathF.Sin(time * 1.5f) * 0.03f;
        _legL.EulerAngles = new Vector3(swing, 0f, 0f);
        _legR.EulerAngles = new Vector3(-swing, 0f, 0f);
        _armL.EulerAngles = new Vector3(-swing * 0.8f, 0f, 0.08f);
        _armR.EulerAngles = new Vector3(swing * 0.8f, 0f, -0.08f);
    }
}

using System.Numerics;
using ThreeNet;

namespace OurHappyHome.Rendering;

/// <summary>
/// A rigged GLB (neighbour, visitor or pet) imported for one instance, so its
/// animation clips drive only this copy, with cross-fades between clips.
/// Returns null from <see cref="TryLoad"/> when the model is missing so the
/// caller can fall back to a <see cref="Mannequin"/> or primitives.
/// </summary>
public sealed class AnimatedFigure
{
    private readonly Dictionary<string, AnimationClip> _clips;
    private AnimationPlayer? _current;
    private AnimationPlayer? _previous;
    private float _fade = 1f;

    private AnimatedFigure(Node root, Dictionary<string, AnimationClip> clips)
    {
        Root = root;
        _clips = clips;
        Jaw = Jaw.Find(root);
    }

    /// <summary>The un-animated jaw bone used for talking, if the rig has one.</summary>
    public Jaw? Jaw { get; }

    /// <summary>The scaled model node; the caller positions its parent.</summary>
    public Node Root { get; }

    public string Current { get; private set; } = "";

    public static AnimatedFigure? TryLoad(Scene scene, Node parent, string modelName, float height)
    {
        string path = ModelLibrary.ModelPath(modelName);
        if (!File.Exists(path))
        {
            return null;
        }

        Node root = parent.CreateChild($"figure-{modelName}");
        try
        {
            int before = scene.Animations.Count;
            ImportResult import = scene.LoadGltf(path, root);
            Dictionary<string, AnimationClip> clips = [];
            foreach (AnimationClip clip in scene.Animations.Skip(before))
            {
                clips[clip.Name] = clip;
            }

            if (import.AnimationCount == 0)
            {
                clips.Clear();
            }

            BoundingBox bounds = scene.GetBounds(import.Root);
            float modelHeight = bounds.Max.Y - bounds.Min.Y;
            if (modelHeight > 0.05f)
            {
                root.Scale = new Vector3(height / modelHeight);
            }

            root.SetShadowsRecursive(true, true);
            AnimatedFigure figure = new(root, clips);
            figure.Play("Idle");
            return figure;
        }
        catch (ThreeNetException)
        {
            root.Remove();
            return null;
        }
    }

    public bool Has(string clip) => _clips.ContainsKey(clip);

    /// <summary>Cross-fades to <paramref name="name"/> (or Idle when the clip is missing).</summary>
    public void Play(string name, float speed = 1f)
    {
        if (Current != name)
        {
            if (!_clips.TryGetValue(name, out AnimationClip? clip) && !_clips.TryGetValue("Idle", out clip))
            {
                Current = name;
                return;
            }

            _previous?.Stop();
            _previous = _current;
            _current = clip.Play(loop: true, speed: speed, weight: _previous is null ? 1f : 0f);
            _fade = _previous is null ? 1f : 0f;
            Current = name;
        }

        if (_current is not null)
        {
            _current.Speed = speed;
        }
    }

    /// <summary>
    /// Stops the clips. Call before removing the nodes: a player left running keeps
    /// writing to its node ids, which ThreeNet reuses for the next nodes created.
    /// </summary>
    public void Stop()
    {
        _previous?.Stop();
        _current?.Stop();
        _previous = null;
        _current = null;
        Current = "";
    }

    public void Update(float dt)
    {
        if (_previous is null)
        {
            return;
        }

        _fade = MathF.Min(1f, _fade + (dt * 5f));
        if (_current is not null)
        {
            _current.Weight = _fade;
        }

        _previous.Weight = 1f - _fade;
        if (_fade >= 1f)
        {
            _previous.Stop();
            _previous = null;
        }
    }
}

/// <summary>
/// The jaw bone of a rig. ThreeNet has no morph targets, so talking is shown by
/// opening the jaw: the rig scripts leave it out of every clip and the game
/// rotates it around its own X axis.
/// </summary>
public sealed class Jaw
{
    private const float MaxAngle = -0.2f;
    private readonly Node _node;
    private readonly Quaternion _rest;
    private float _open;

    private Jaw(Node node)
    {
        _node = node;
        _rest = node.Rotation;
    }

    public static Jaw? Find(Node root)
    {
        Stack<Node> stack = new([root]);
        while (stack.Count > 0)
        {
            Node node = stack.Pop();
            if (node.Name == "Jaw")
            {
                return new Jaw(node);
            }

            foreach (Node child in node.Children)
            {
                stack.Push(child);
            }
        }

        return null;
    }

    /// <summary>Chatters while <paramref name="talking"/> (syllable-like rhythm), otherwise eases shut.</summary>
    public void Update(bool talking, float time, float dt, float agape = 0f)
    {
        float target = agape;
        if (talking)
        {
            float syllable = MathF.Max(0f, MathF.Sin(time * 17f));
            float phrase = 0.55f + (0.45f * MathF.Sin(time * 5.3f));
            target = MathF.Max(agape, syllable * phrase);
        }

        _open += (target - _open) * MathF.Min(1f, dt * 25f);
        _node.Rotation = _rest * Quaternion.CreateFromAxisAngle(Vector3.UnitX, _open * MaxAngle);
    }
}

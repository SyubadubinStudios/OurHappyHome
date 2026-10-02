using System.Numerics;
using ThreeNet;

namespace OurHappyHome.Rendering;

/// <summary>
/// Rodin generated props: each GLB is imported once into a hidden prototype
/// and placed with <see cref="Node.Clone"/>, so instances share GPU buffers.
/// Missing files return null and callers fall back to primitives.
/// </summary>
public sealed class ModelLibrary
{
    private readonly Scene _scene;
    private readonly Node _prototypes;
    private readonly Dictionary<string, Prototype?> _cache = [];

    private sealed record Prototype(Node Root, Vector3 Size, Vector3 Centre, float Bottom);

    public ModelLibrary(Scene scene)
    {
        _scene = scene;
        _prototypes = scene.CreateNode(null, "model-prototypes");
        _prototypes.Visible = false;
    }

    public static string ModelPath(string name) => Path.Combine(AppContext.BaseDirectory, "Assets", "Models", name + ".glb");

    public static bool Exists(string name) => File.Exists(ModelPath(name));

    private Prototype? Load(string name)
    {
        if (_cache.TryGetValue(name, out Prototype? cached))
        {
            return cached;
        }

        Prototype? prototype = null;
        string path = ModelPath(name);
        if (File.Exists(path))
        {
            try
            {
                Node holder = _scene.CreateNode(_prototypes, name);
                ImportResult import = _scene.LoadGltf(path, holder);
                BoundingBox bounds = _scene.GetBounds(import.Root);
                prototype = new Prototype(holder, bounds.Max - bounds.Min, (bounds.Min + bounds.Max) * 0.5f, bounds.Min.Y);
            }
            catch (ThreeNetException)
            {
                prototype = null;
            }
        }

        _cache[name] = prototype;
        return prototype;
    }

    /// <summary>
    /// Places a copy scaled uniformly to fit inside <paramref name="footprint"/>
    /// (X × Z), standing on the parent's origin. Returns the pivot or null.
    /// </summary>
    public Node? Fit(string name, Node parent, Vector2 footprint, float maxHeight = float.MaxValue, float yawOffset = 0f)
    {
        if (Load(name) is not { } prototype)
        {
            return null;
        }

        Vector3 size = prototype.Size;
        bool quarter = MathF.Abs(MathF.Sin(yawOffset)) > 0.7f;
        float sx = quarter ? size.Z : size.X;
        float sz = quarter ? size.X : size.Z;
        float scale = MathF.Min(footprint.X / MathF.Max(sx, 1e-3f), footprint.Y / MathF.Max(sz, 1e-3f));
        scale = MathF.Min(scale, maxHeight / MathF.Max(size.Y, 1e-3f));
        return Attach(prototype, parent, scale, yawOffset);
    }

    /// <summary>Places a copy scaled to the given height.</summary>
    public Node? Height(string name, Node parent, float height, float yawOffset = 0f)
    {
        if (Load(name) is not { } prototype)
        {
            return null;
        }

        return Attach(prototype, parent, height / MathF.Max(prototype.Size.Y, 1e-3f), yawOffset);
    }

    private static Node Attach(Prototype prototype, Node parent, float scale, float yawOffset)
    {
        Node pivot = parent.CreateChild("model");
        pivot.EulerAngles = new Vector3(0f, yawOffset, 0f);
        Node copy = prototype.Root.Clone(pivot);
        copy.Visible = true;
        copy.Scale = new Vector3(scale);
        copy.Position = -new Vector3(prototype.Centre.X * scale, prototype.Bottom * scale, prototype.Centre.Z * scale);
        return pivot;
    }
}

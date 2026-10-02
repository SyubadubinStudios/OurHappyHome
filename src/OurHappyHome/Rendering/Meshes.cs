using System.Numerics;
using ThreeNet;
using ThreeNet.Interop;

namespace OurHappyHome.Rendering;

/// <summary>
/// Shared unit primitives scaled per node, so hundreds of walls, trees and
/// props reuse a handful of GPU buffers. Helpers place them in world units.
/// </summary>
public sealed class Meshes
{
    private readonly Scene _scene;
    private readonly Dictionary<(float, float, float, float), (Geometry Slopes, Geometry Gables)> _roofs = [];

    public Meshes(Scene scene)
    {
        _scene = scene;
        UnitBox = scene.CreateBoxGeometry(1f, 1f, 1f);
        UnitCylinder = scene.CreateCylinderGeometry(0.5f, 0.5f, 1f, 16);
        UnitCylinderLow = scene.CreateCylinderGeometry(0.5f, 0.5f, 1f, 8);
        UnitSphere = scene.CreateSphereGeometry(0.5f, 16, 10);
        UnitSphereLow = scene.CreateSphereGeometry(0.5f, 10, 6);
        UnitCone = scene.CreateConeGeometry(0.5f, 1f, 12);
        UnitPyramid = scene.CreateConeGeometry(0.5f, 1f, 4);
        UnitPlane = scene.CreatePlaneGeometry(1f, 1f);
        UnitDisc = scene.CreateCylinderGeometry(0.5f, 0.5f, 1f, 24);
        Torus = scene.CreateTorusGeometry(1f, 0.06f, 8, 32);
    }

    public Scene Scene => _scene;

    public Geometry UnitBox { get; }
    public Geometry UnitCylinder { get; }
    public Geometry UnitCylinderLow { get; }
    public Geometry UnitSphere { get; }
    public Geometry UnitSphereLow { get; }
    public Geometry UnitCone { get; }
    public Geometry UnitPyramid { get; }
    public Geometry UnitPlane { get; }
    public Geometry UnitDisc { get; }
    public Geometry Torus { get; }

    public Node Box(Node? parent, Vector3 center, Vector3 size, Material material, string name = "box")
    {
        Node node = _scene.AddMesh(UnitBox, material, parent, name);
        node.Position = center;
        node.Scale = size;
        return node;
    }

    /// <summary>Box standing on <paramref name="baseY"/> with footprint centred at (x, z).</summary>
    public Node Block(Node? parent, float x, float z, float baseY, Vector3 size, Material material, string name = "block") =>
        Box(parent, new Vector3(x, baseY + (size.Y / 2f), z), size, material, name);

    public Node Cylinder(Node? parent, Vector3 center, float radius, float height, Material material, bool low = false, string name = "cyl")
    {
        Node node = _scene.AddMesh(low ? UnitCylinderLow : UnitCylinder, material, parent, name);
        node.Position = center;
        node.Scale = new Vector3(radius * 2f, height, radius * 2f);
        return node;
    }

    public Node Sphere(Node? parent, Vector3 center, Vector3 size, Material material, bool low = false, string name = "sphere")
    {
        Node node = _scene.AddMesh(low ? UnitSphereLow : UnitSphere, material, parent, name);
        node.Position = center;
        node.Scale = size;
        return node;
    }

    public Node Cone(Node? parent, Vector3 baseCenter, float radius, float height, Material material, bool pyramid = false, string name = "cone")
    {
        Node node = _scene.AddMesh(pyramid ? UnitPyramid : UnitCone, material, parent, name);
        node.Position = baseCenter + new Vector3(0f, height / 2f, 0f);
        node.Scale = new Vector3(radius * 2f, height, radius * 2f);
        return node;
    }

    /// <summary>Horizontal quad facing up (floors, lawns, roads).</summary>
    public Node Ground(Node? parent, Vector3 center, Vector2 size, Material material, string name = "ground")
    {
        Node node = _scene.AddMesh(UnitPlane, material, parent, name);
        node.Position = center;
        node.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2f);
        node.Scale = new Vector3(size.X, size.Y, 1f);
        node.CastShadow = false;
        return node;
    }

    /// <summary>Upright quad facing +Z (signs, billboards before rotation).</summary>
    public Node Quad(Node? parent, Vector3 center, Vector2 size, Material material, string name = "quad")
    {
        Node node = _scene.AddMesh(UnitPlane, material, parent, name);
        node.Position = center;
        node.Scale = new Vector3(size.X, size.Y, 1f);
        node.CastShadow = false;
        return node;
    }

    /// <summary>
    /// Gable roof over width × depth (ridge along X) with overhang. Local origin
    /// is the centre of the eaves line. Geometry is cached by its dimensions.
    /// </summary>
    public (Geometry Slopes, Geometry Gables) GableRoof(float width, float depth, float rise, float overhang)
    {
        (float, float, float, float) key = (MathF.Round(width, 2), MathF.Round(depth, 2), MathF.Round(rise, 2), MathF.Round(overhang, 2));
        if (_roofs.TryGetValue(key, out (Geometry, Geometry) cached))
        {
            return cached;
        }

        float hw = (width * 0.5f) + overhang;
        float hd = (depth * 0.5f) + overhang;
        float gw = width * 0.5f;
        float gd = depth * 0.5f;

        ShapeBuilder slopes = new();
        slopes.Quad(new(-hw, 0f, hd), new(hw, 0f, hd), new(hw, rise, 0f), new(-hw, rise, 0f), 0.5f);
        slopes.Quad(new(hw, 0f, -hd), new(-hw, 0f, -hd), new(-hw, rise, 0f), new(hw, rise, 0f), 0.5f);
        slopes.Quad(new(-hw, -0.03f, hd), new(-hw, rise - 0.03f, 0f), new(hw, rise - 0.03f, 0f), new(hw, -0.03f, hd), 0.5f);
        slopes.Quad(new(hw, -0.03f, -hd), new(hw, rise - 0.03f, 0f), new(-hw, rise - 0.03f, 0f), new(-hw, -0.03f, -hd), 0.5f);

        // Gable ends: a pentagon from the wall top up to the underside of the roof.
        ShapeBuilder gables = new();
        float edge = rise * (1f - (gd / hd));
        foreach (float x in new[] { gw, -gw })
        {
            Vector3[] pentagon = [new(x, 0f, gd), new(x, 0f, -gd), new(x, edge, -gd), new(x, rise - 0.02f, 0f), new(x, edge, gd)];
            gables.Polygon(pentagon, 0.5f);
            gables.Polygon([.. pentagon.Reverse()], 0.5f);
        }

        (Geometry, Geometry) result = (slopes.Build(_scene), gables.Build(_scene));
        _roofs[key] = result;
        return result;
    }

    /// <summary>Adds a gable roof node: ridge along the longer side of the footprint.</summary>
    public Node Roof(Node? parent, Vector2 center, Vector2 size, float baseY, float rise, float overhang, Material tiles, Material gable)
    {
        bool alongX = size.X >= size.Y;
        (Geometry slopes, Geometry gables) = GableRoof(alongX ? size.X : size.Y, alongX ? size.Y : size.X, rise, overhang);
        Node root = _scene.CreateNode(parent, "roof");
        root.Position = new Vector3(center.X, baseY, center.Y);
        root.EulerAngles = new Vector3(0f, alongX ? 0f : MathF.PI / 2f, 0f);
        _scene.AddMesh(slopes, tiles, root, "roof-slopes");
        _scene.AddMesh(gables, gable, root, "roof-gables");
        return root;
    }
}

/// <summary>Builds custom meshes from polygons (roofs, gables).</summary>
public sealed class ShapeBuilder
{
    private readonly List<Vertex> _vertices = [];
    private readonly List<uint> _indices = [];

    public void Polygon(IReadOnlyList<Vector3> points, float uvScale = 1f)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(points[1] - points[0], points[2] - points[0]));
        Vector3 tangent = Vector3.Normalize(points[1] - points[0]);
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        uint start = (uint)_vertices.Count;
        foreach (Vector3 point in points)
        {
            Vector3 local = point - points[0];
            _vertices.Add(new Vertex(point, normal, new Vector2(Vector3.Dot(local, tangent), Vector3.Dot(local, bitangent)) * uvScale));
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            _indices.Add(start);
            _indices.Add(start + (uint)i);
            _indices.Add(start + (uint)i + 1);
        }
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale = 1f) => Polygon([a, b, c, d], uvScale);

    public Geometry Build(Scene scene)
    {
        Geometry geometry = scene.CreateGeometry(_vertices.ToArray(), _indices.ToArray());
        geometry.ComputeTangents();
        return geometry;
    }
}

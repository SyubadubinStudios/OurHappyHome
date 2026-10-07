using System.Numerics;
using OurHappyHome.Core.World;
using ThreeNet;

namespace OurHappyHome.Rendering;

/// <summary>
/// Builds the look of each furniture type: a fitted Rodin model when one
/// exists, otherwise a stylised primitive build in the game's palette.
/// Local frame: front faces +Z, standing on y = 0.
/// </summary>
public static class FurnitureFactory
{
    private static readonly Dictionary<string, string> ModelFor = new()
    {
        ["sofa"] = "sofa",
        ["tv"] = "tv-cabinet",
        ["bookshelf"] = "bookshelf",
        ["kitchen"] = "kitchen",
        ["fridge"] = "fridge",
        ["dining"] = "dining-set",
        ["double-bed"] = "double-bed",
        ["bunk-bed"] = "bunk-bed",
        ["desk"] = "desk",
        ["car"] = "car",
        ["swing-set"] = "swing-set",
        ["flower-bush"] = "flower-bush",
    };

    private static readonly Dictionary<string, float> YawOffset = new()
    {
        ["bunk-bed"] = MathF.PI / 2f,
    };

    public static void Build(FurnitureDef def, Node node, Meshes m, Textures t, ModelLibrary models, int variant)
    {
        if (ModelFor.TryGetValue(def.Id, out string? model) &&
            models.Fit(model, node, def.Footprint, def.Height * 1.25f, YawOffset.GetValueOrDefault(def.Id)) is not null)
        {
            node.SetShadowsRecursive(true, true);
            return;
        }

        Vector2 f = def.Footprint;
        float h = def.Height;
        Material wood = t.Solid("#B88452", 0.6f);
        Material darkWood = t.Solid("#7A5230", 0.6f);
        Material white = t.Solid("#F4F4F2", 0.35f);
        Material metal = t.Solid("#B9C2CC", 0.35f, 0.6f);
        Material main = t.Solid(def.Color, 0.6f);

        switch (def.Id)
        {
            case "tree":
                Tree(node, m, t, variant, models);
                break;
            case "armchair":
                m.Block(node, 0f, 0f, 0.05f, new(f.X, 0.38f, f.Y), main);
                m.Block(node, 0f, -f.Y / 2f + 0.1f, 0.4f, new(f.X, 0.5f, 0.2f), main);
                m.Block(node, -f.X / 2f + 0.08f, 0f, 0.4f, new(0.16f, 0.22f, f.Y), main);
                m.Block(node, f.X / 2f - 0.08f, 0f, 0.4f, new(0.16f, 0.22f, f.Y), main);
                m.Block(node, 0f, 0.05f, 0.43f, new(f.X - 0.3f, 0.1f, f.Y - 0.3f), t.Solid("#F2D9A8", 0.9f));
                break;
            case "coffee-table":
                m.Block(node, 0f, 0f, 0.36f, new(f.X, 0.06f, f.Y), wood);
                foreach ((float x, float z) in Corners(f, 0.08f))
                {
                    m.Block(node, x, z, 0f, new(0.06f, 0.36f, 0.06f), darkWood);
                }

                m.Sphere(node, new Vector3(0.2f, 0.45f, 0f), new(0.14f, 0.1f, 0.14f), t.Solid("#E8A33D"));
                break;
            case "rug":
                {
                    Node rug = m.Scene.AddMesh(m.UnitDisc, t.Solid(def.Color, 0.95f), node, "rug");
                    rug.Position = new Vector3(0f, 0.025f, 0f);
                    rug.Scale = new Vector3(f.X, 0.01f, f.Y);
                    rug.CastShadow = false;
                    Node inner = m.Scene.AddMesh(m.UnitDisc, t.Solid("#F3D3B6", 0.95f), node, "rug-inner");
                    inner.Position = new Vector3(0f, 0.03f, 0f);
                    inner.Scale = new Vector3(f.X * 0.65f, 0.01f, f.Y * 0.65f);
                    inner.CastShadow = false;
                    break;
                }

            case "lamp":
                m.Cylinder(node, new Vector3(0f, 0.02f, 0f), 0.16f, 0.04f, darkWood);
                m.Cylinder(node, new Vector3(0f, 0.75f, 0f), 0.025f, 1.45f, metal, true);
                m.Cone(node, new Vector3(0f, 1.32f, 0f), 0.22f, 0.3f, t.LampGlow);
                break;
            case "aquarium":
                {
                    m.Block(node, 0f, 0f, 0f, new(f.X, 0.55f, f.Y), darkWood);
                    Node glass = m.Block(node, 0f, 0f, 0.55f, new(f.X, 0.55f, f.Y), t.Solid("#8FD3F0", 0.05f, emissive: 0.35f));
                    glass.CastShadow = false;
                    m.Block(node, 0f, 0f, 0.55f, new(f.X - 0.04f, 0.05f, f.Y - 0.04f), t.Solid("#E9D8A6", 0.9f));
                    foreach ((float x, float y, string c) in new[] { (-0.25f, 0.8f, "#FF8C42"), (0.15f, 0.92f, "#FFD166"), (0.3f, 0.74f, "#EF476F") })
                    {
                        m.Sphere(node, new Vector3(x, y, f.Y / 2f + 0.01f), new(0.12f, 0.07f, 0.03f), t.Solid(c, 0.4f, emissive: 0.3f), true);
                    }

                    m.Block(node, 0f, 0f, 1.1f, new(f.X + 0.02f, 0.05f, f.Y + 0.02f), darkWood);
                    break;
                }

            case "beanbag":
                m.Sphere(node, new Vector3(0f, 0.25f, 0f), new(f.X, 0.5f, f.Y), main, true);
                m.Sphere(node, new Vector3(0f, 0.42f, -0.25f), new(f.X * 0.85f, 0.4f, 0.45f), main, true);
                break;
            case "painting":
                m.Block(node, 0f, 0f, 1.25f, new(f.X, 0.7f, f.Y * 0.5f), darkWood);
                m.Block(node, 0f, f.Y * 0.26f, 1.3f, new(f.X - 0.1f, 0.6f, 0.01f), t.Solid("#BEE3F8", 0.9f));
                m.Block(node, 0f, f.Y * 0.27f, 1.3f, new(f.X - 0.1f, 0.22f, 0.01f), t.Solid("#7CB518", 0.9f));
                m.Sphere(node, new Vector3(0.22f, 1.72f, f.Y * 0.28f), new(0.14f, 0.14f, 0.01f), t.Solid("#FFD166", 0.6f, emissive: 0.4f), true);
                break;
            case "arcade":
                m.Block(node, 0f, 0f, 0f, new(f.X, h, f.Y), main);
                m.Block(node, 0f, f.Y / 2f, 1.05f, new(f.X - 0.15f, 0.5f, 0.02f), t.Solid("#1B1B2F", 0.2f, emissive: 0.2f));
                m.Block(node, 0f, f.Y / 2f + 0.01f, 1.1f, new(f.X - 0.25f, 0.38f, 0.01f), t.Solid("#4CC9F0", 0.3f, emissive: 1.4f));
                m.Block(node, 0f, f.Y / 2f + 0.12f, 0.85f, new(f.X - 0.1f, 0.06f, 0.25f), t.Solid("#2B2D42"));
                m.Sphere(node, new Vector3(-0.15f, 0.92f, f.Y / 2f + 0.15f), new(0.07f), t.Solid("#EF476F", 0.4f), true);
                m.Sphere(node, new Vector3(0.12f, 0.9f, f.Y / 2f + 0.15f), new(0.06f), t.Solid("#FFD166", 0.4f), true);
                m.Block(node, 0f, f.Y / 2f + 0.01f, 1.62f, new(f.X - 0.1f, 0.14f, 0.01f), t.Solid("#FF9F1C", 0.5f, emissive: 1f));
                break;
            case "hammock":
                foreach (float z in new[] { -f.Y / 2f + 0.06f, f.Y / 2f - 0.06f })
                {
                    m.Block(node, -0.35f, z, 0f, new(0.08f, h, 0.08f), darkWood);
                    m.Block(node, 0.35f, z, 0f, new(0.08f, h, 0.08f), darkWood);
                    m.Block(node, 0f, z, h - 0.08f, new(0.8f, 0.08f, 0.08f), darkWood);
                }

                m.Block(node, 0f, 0f, 0.5f, new(0.75f, 0.05f, f.Y - 0.5f), main);
                m.Block(node, 0f, 0f, 0.55f, new(0.72f, 0.03f, f.Y - 0.55f), t.Solid("#E76F51", 0.9f));
                break;
            case "plant":
                m.Cylinder(node, new Vector3(0f, 0.18f, 0f), 0.18f, 0.36f, t.Solid("#C96F4A", 0.8f));
                m.Sphere(node, new Vector3(0f, 0.62f, 0f), new(0.55f, 0.6f, 0.55f), t.Solid("#4F9D4F", 0.8f), true);
                m.Sphere(node, new Vector3(0.12f, 0.88f, 0.05f), new(0.35f, 0.4f, 0.35f), t.Solid("#62B35E", 0.8f), true);
                break;
            case "piano":
                m.Block(node, 0f, 0f, 0.65f, new(f.X, 0.12f, f.Y), main);
                m.Block(node, 0f, 0.02f, 0.77f, new(f.X - 0.1f, 0.02f, f.Y * 0.5f), white);
                m.Block(node, -f.X / 2f + 0.1f, 0f, 0f, new(0.06f, 0.65f, 0.3f), main);
                m.Block(node, f.X / 2f - 0.1f, 0f, 0f, new(0.06f, 0.65f, 0.3f), main);
                break;
            case "blender":
                m.Block(node, 0f, 0f, 0f, new(f.X, 0.9f, f.Y), t.Solid("#E9F1F2"));
                m.Cylinder(node, new Vector3(0f, 1.02f, 0f), 0.08f, 0.16f, t.Solid(def.Color));
                m.Cylinder(node, new Vector3(0f, 1.2f, 0f), 0.07f, 0.22f, t.Glass);
                break;
            case "washer":
                m.Block(node, 0f, 0f, 0f, new(f.X, h, f.Y), white);
                {
                    Node door = m.Scene.AddMesh(m.UnitDisc, t.Solid("#7FA7C9", 0.1f, 0.3f), node, "washer-door");
                    door.Position = new Vector3(0f, h * 0.45f, f.Y / 2f + 0.01f);
                    door.EulerAngles = new Vector3(MathF.PI / 2f, 0f, 0f);
                    door.Scale = new Vector3(0.42f, 0.02f, 0.42f);
                }

                break;
            case "single-bed":
                m.Block(node, 0f, 0f, 0.05f, new(f.X, 0.3f, f.Y), wood);
                m.Block(node, 0f, 0.05f, 0.35f, new(f.X - 0.08f, 0.16f, f.Y - 0.1f), white);
                m.Block(node, 0f, 0.25f, 0.49f, new(f.X - 0.06f, 0.06f, f.Y * 0.6f), main);
                m.Block(node, 0f, -f.Y / 2f + 0.25f, 0.5f, new(f.X * 0.6f, 0.12f, 0.3f), white);
                m.Block(node, 0f, -f.Y / 2f + 0.04f, 0f, new(f.X, 0.95f, 0.08f), wood);
                break;
            case "wardrobe":
                m.Block(node, 0f, 0f, 0f, new(f.X, h, f.Y), main);
                m.Block(node, 0f, f.Y / 2f + 0.005f, 0.1f, new(0.02f, h - 0.25f, 0.01f), darkWood);
                m.Sphere(node, new Vector3(-0.08f, h * 0.5f, f.Y / 2f + 0.03f), new(0.05f), metal);
                m.Sphere(node, new Vector3(0.08f, h * 0.5f, f.Y / 2f + 0.03f), new(0.05f), metal);
                break;
            case "toy-box":
                m.Block(node, 0f, 0f, 0f, new(f.X, h, f.Y), main);
                m.Block(node, 0f, 0f, h, new(f.X + 0.04f, 0.06f, f.Y + 0.04f), t.Solid("#F7D547"));
                m.Sphere(node, new Vector3(-0.2f, h + 0.15f, 0f), new(0.22f), t.Solid("#3C7DD9"));
                m.Block(node, 0.2f, 0.05f, h + 0.03f, new(0.2f, 0.2f, 0.2f), t.Solid("#5DBB63"));
                break;
            case "easel":
                m.Block(node, -0.25f, 0f, 0f, new(0.05f, h, 0.05f), wood).EulerAngles = new Vector3(0f, 0f, 0.12f);
                m.Block(node, 0.25f, 0f, 0f, new(0.05f, h, 0.05f), wood).EulerAngles = new Vector3(0f, 0f, -0.12f);
                m.Block(node, 0f, 0.05f, 0.75f, new(0.62f, 0.5f, 0.03f), t.Solid("#FFFDF5", 0.9f));
                m.Sphere(node, new Vector3(-0.1f, 1.05f, 0.08f), new(0.14f, 0.14f, 0.02f), t.Solid("#E85D75"));
                m.Sphere(node, new Vector3(0.12f, 0.95f, 0.08f), new(0.12f, 0.12f, 0.02f), t.Solid("#3C7DD9"));
                break;
            case "dog-bed":
                {
                    Node ring = m.Scene.AddMesh(m.Torus, main, node, "dog-bed");
                    ring.Position = new Vector3(0f, 0.08f, 0f);
                    ring.Scale = new Vector3(f.X * 0.45f, 1.6f, f.Y * 0.45f);
                    Node cushion = m.Scene.AddMesh(m.UnitDisc, t.Solid("#F3E3C3", 0.95f), node, "cushion");
                    cushion.Position = new Vector3(0f, 0.04f, 0f);
                    cushion.Scale = new Vector3(f.X * 0.85f, 0.08f, f.Y * 0.85f);
                    break;
                }

            case "toilet":
                m.Block(node, 0f, -f.Y / 2f + 0.1f, 0.4f, new(f.X * 0.9f, 0.4f, 0.18f), white);
                m.Cylinder(node, new Vector3(0f, 0.2f, 0.08f), 0.2f, 0.4f, white);
                m.Cylinder(node, new Vector3(0f, 0.42f, 0.08f), 0.21f, 0.04f, t.Solid("#DDE7EE"));
                break;
            case "shower":
                m.Block(node, 0f, 0f, 0f, new(f.X, h, f.Y), white);
                m.Block(node, 0f, 0f, h - 0.02f, new(f.X - 0.12f, 0.02f, f.Y - 0.12f), t.Solid("#9ED7F0", 0.1f));
                m.Cylinder(node, new Vector3(f.X / 2f - 0.15f, 1.3f, -f.Y / 2f + 0.06f), 0.02f, 1.4f, metal, true);
                m.Cylinder(node, new Vector3(f.X / 2f - 0.15f, 2.0f, -f.Y / 2f + 0.16f), 0.09f, 0.03f, metal);
                break;
            case "sink":
                m.Block(node, 0f, 0f, 0f, new(f.X * 0.5f, 0.75f, f.Y * 0.5f), white);
                m.Block(node, 0f, 0f, 0.75f, new(f.X, 0.15f, f.Y), white);
                m.Cylinder(node, new Vector3(0f, 0.95f, -f.Y / 2f + 0.08f), 0.02f, 0.18f, metal, true);
                m.Block(node, 0f, -f.Y / 2f + 0.02f, 1.1f, new(f.X * 0.8f, 0.6f, 0.03f), t.Solid("#CFE8F2", 0.05f, 0.5f));
                break;
            case "fuse-box":
                m.Block(node, 0f, 0f, 1.3f, new(f.X, 0.6f, f.Y), main);
                m.Block(node, 0f, f.Y / 2f, 1.45f, new(0.3f, 0.2f, 0.02f), t.Solid("#F2C14E", emissive: 0.4f));
                break;
            case "security":
                m.Block(node, 0f, 0f, 1.3f, new(f.X, 0.45f, f.Y), main);
                m.Block(node, 0f, f.Y / 2f, 1.35f, new(0.36f, 0.28f, 0.02f), t.Solid("#2BD38F", emissive: 1.2f));
                break;
            case "extinguisher":
                m.Cylinder(node, new Vector3(0f, 0.35f, 0f), 0.11f, 0.6f, main);
                m.Cylinder(node, new Vector3(0f, 0.72f, 0f), 0.04f, 0.12f, t.Solid("#222222"), true);
                break;
            case "first-aid":
                m.Block(node, 0f, 0f, 1.2f, new(f.X, 0.32f, f.Y), main);
                m.Block(node, 0f, f.Y / 2f + 0.01f, 1.31f, new(0.08f, 0.22f, 0.01f), t.Solid("#E03C31"));
                m.Block(node, 0f, f.Y / 2f + 0.01f, 1.31f + 0.07f, new(0.22f, 0.08f, 0.01f), t.Solid("#E03C31"));
                break;
            case "workbench":
                m.Block(node, 0f, 0f, h - 0.08f, new(f.X, 0.08f, f.Y), wood);
                foreach ((float x, float z) in Corners(f, 0.08f))
                {
                    m.Block(node, x, z, 0f, new(0.08f, h - 0.08f, 0.08f), darkWood);
                }

                m.Block(node, 0f, -f.Y / 2f + 0.03f, h, new(f.X, 0.8f, 0.04f), t.Solid("#C9A27E"));
                m.Block(node, -0.4f, 0f, h, new(0.3f, 0.12f, 0.2f), t.Solid("#D93A2B"));
                break;
            case "computer":
                m.Block(node, 0f, 0f, 0.72f, new(f.X, 0.05f, f.Y), wood);
                m.Block(node, -f.X / 2f + 0.06f, 0f, 0f, new(0.05f, 0.72f, f.Y), wood);
                m.Block(node, f.X / 2f - 0.06f, 0f, 0f, new(0.05f, 0.72f, f.Y), wood);
                m.Block(node, 0f, -0.15f, 0.77f, new(0.6f, 0.4f, 0.04f), t.Solid("#1E2630"));
                m.Block(node, 0f, -0.13f, 0.84f, new(0.54f, 0.32f, 0.01f), t.Solid("#5EC4E8", emissive: 0.8f));
                break;
            case "telescope":
                m.Cylinder(node, new Vector3(0f, 0.5f, 0f), 0.03f, 1f, metal, true);
                m.Cylinder(node, new Vector3(0f, 1.15f, 0.1f), 0.09f, 0.7f, main).EulerAngles = new Vector3(0.9f, 0f, 0f);
                break;
            case "veg-patch":
                m.Block(node, 0f, 0f, 0f, new(f.X, 0.22f, f.Y), t.Solid("#8D6E4F"));
                m.Block(node, 0f, 0f, 0.22f, new(f.X - 0.1f, 0.03f, f.Y - 0.1f), t.Dirt);
                for (int i = 0; i < 8; i++)
                {
                    float x = -f.X / 2f + 0.25f + ((i % 4) * (f.X - 0.5f) / 3f);
                    float z = i < 4 ? -0.25f : 0.25f;
                    m.Sphere(node, new Vector3(x, 0.34f, z), new(0.22f, 0.2f, 0.22f), t.Solid(i % 3 == 0 ? "#E8692E" : "#4FA35A"), true);
                }

                break;
            case "lemonade":
                m.Block(node, 0f, 0f, 0f, new(f.X, 0.9f, f.Y), t.Solid("#F2F0E6"));
                m.Block(node, 0f, 0.02f, 0.9f, new(f.X + 0.1f, 0.05f, f.Y + 0.1f), main);
                m.Block(node, -f.X / 2f + 0.05f, 0f, 0.9f, new(0.05f, 0.7f, 0.05f), wood);
                m.Block(node, f.X / 2f - 0.05f, 0f, 0.9f, new(0.05f, 0.7f, 0.05f), wood);
                m.Block(node, 0f, 0f, 1.6f, new(f.X + 0.2f, 0.06f, f.Y + 0.2f), t.Solid("#F25F5C"));
                m.Cylinder(node, new Vector3(0.3f, 1.05f, 0f), 0.12f, 0.25f, t.Solid("#FFE66D", 0.2f));
                break;
            case "bbq":
                m.Cylinder(node, new Vector3(0f, 0.75f, 0f), f.X / 2f, 0.3f, main, true);
                m.Block(node, 0f, 0f, 0f, new(0.05f, 0.65f, 0.05f), metal);
                m.Block(node, 0f, 0f, 0.9f, new(f.X - 0.1f, 0.02f, f.Y - 0.1f), t.Solid("#FF7A2E", emissive: 1.5f));
                break;
            case "trampoline":
                {
                    Node ring = m.Scene.AddMesh(m.Torus, t.Solid("#3C7DD9"), node, "tramp-ring");
                    ring.Position = new Vector3(0f, h, 0f);
                    ring.Scale = new Vector3(f.X * 0.5f, 1.5f, f.Y * 0.5f);
                    Node mat = m.Scene.AddMesh(m.UnitDisc, t.Solid("#22252B", 0.9f), node, "tramp-mat");
                    mat.Position = new Vector3(0f, h, 0f);
                    mat.Scale = new Vector3(f.X * 0.92f, 0.02f, f.Y * 0.92f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * MathF.Tau / 6f;
                        m.Block(node, MathF.Cos(a) * f.X * 0.45f, MathF.Sin(a) * f.Y * 0.45f, 0f, new(0.06f, h, 0.06f), metal);
                    }

                    break;
                }

            case "picnic":
                m.Block(node, 0f, 0f, 0.7f, new(f.X, 0.06f, 0.9f), wood);
                m.Block(node, 0f, 0.65f, 0.4f, new(f.X, 0.05f, 0.3f), wood);
                m.Block(node, 0f, -0.65f, 0.4f, new(f.X, 0.05f, 0.3f), wood);
                foreach ((float x, float z) in Corners(new Vector2(f.X, 0.9f), 0.15f))
                {
                    m.Block(node, x, z, 0f, new(0.07f, 0.7f, 0.07f), darkWood);
                }

                m.Block(node, 0.2f, 0f, 0.76f, new(0.6f, 0.01f, 0.5f), t.Solid("#E05A47", 0.9f));
                break;
            case "swing-set":
                m.Block(node, -1.4f, 0f, 0f, new(0.1f, 2f, 0.1f), t.Solid("#E05A47"));
                m.Block(node, 1.4f, 0f, 0f, new(0.1f, 2f, 0.1f), t.Solid("#E05A47"));
                m.Block(node, 0f, 0f, 2f, new(2.9f, 0.1f, 0.1f), t.Solid("#3C7DD9"));
                break;
            default:
                // Generic coloured block with a darker top.
                m.Block(node, 0f, 0f, 0f, new(f.X, MathF.Max(h, 0.05f), f.Y), main);
                break;
        }

        node.SetShadowsRecursive(def.Height > 0.1f, true);
    }

    public static void Tree(Node node, Meshes m, Textures t, int variant, ModelLibrary models)
    {
        if (variant % 3 == 0 && models.Height("flower-bush", node, 4.2f) is not null)
        {
            return;
        }

        // Rodin shade tree, varied in size and rotation.
        if (variant % 3 == 1 && models.Height("tree", node, 4.4f + (variant % 5 * 0.25f), variant * 1.3f) is not null)
        {
            return;
        }

        Material trunk = t.Solid("#7A5230", 0.9f);
        Material leaves = t.Solid(variant % 2 == 0 ? "#4E9A4B" : "#5DAA45", 0.85f);
        Material leaves2 = t.Solid(variant % 2 == 0 ? "#63B35A" : "#78C05A", 0.85f);
        m.Cylinder(node, new Vector3(0f, 1.1f, 0f), 0.2f, 2.2f, trunk, true);
        m.Sphere(node, new Vector3(0f, 3.0f, 0f), new(3.0f, 2.6f, 3.0f), leaves, true);
        m.Sphere(node, new Vector3(0.7f, 2.5f, 0.4f), new(1.9f, 1.7f, 1.9f), leaves2, true);
        m.Sphere(node, new Vector3(-0.6f, 3.6f, -0.3f), new(1.7f, 1.5f, 1.7f), leaves2, true);
    }

    private static IEnumerable<(float X, float Z)> Corners(Vector2 f, float inset)
    {
        float x = (f.X / 2f) - inset;
        float z = (f.Y / 2f) - inset;
        yield return (-x, -z);
        yield return (x, -z);
        yield return (-x, z);
        yield return (x, z);
    }
}

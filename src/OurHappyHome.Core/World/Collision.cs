using System.Numerics;

namespace OurHappyHome.Core.World;

/// <summary>
/// 2D collision for characters: circles against axis aligned boxes (walls,
/// furniture, buildings, trees), indexed in a uniform grid. The world is flat,
/// so this replaces 3D raycasts and keeps movement cheap and deterministic.
/// </summary>
public sealed class CollisionWorld
{
    private const float Cell = 8f;
    private readonly Dictionary<(int, int), List<int>> _grid = [];
    private readonly List<Rect> _static = [];
    private readonly List<Rect> _dynamic = [];

    public int StaticCount => _static.Count;

    public void ClearStatic()
    {
        _static.Clear();
        _grid.Clear();
    }

    public void AddStatic(Rect rect)
    {
        int index = _static.Count;
        _static.Add(rect);
        foreach ((int, int) cell in Cells(rect))
        {
            if (!_grid.TryGetValue(cell, out List<int>? list))
            {
                list = [];
                _grid[cell] = list;
            }

            list.Add(index);
        }
    }

    /// <summary>Dynamic obstacles (house walls, furniture) are rebuilt when the house changes.</summary>
    public void SetDynamic(IEnumerable<Rect> rects)
    {
        _dynamic.Clear();
        _dynamic.AddRange(rects);
    }

    public IReadOnlyList<Rect> Dynamic => _dynamic;

    private static IEnumerable<(int, int)> Cells(Rect r)
    {
        for (int x = (int)MathF.Floor(r.X0 / Cell); x <= (int)MathF.Floor(r.X1 / Cell); x++)
        {
            for (int z = (int)MathF.Floor(r.Z0 / Cell); z <= (int)MathF.Floor(r.Z1 / Cell); z++)
            {
                yield return (x, z);
            }
        }
    }

    private IEnumerable<Rect> Near(Vector2 p, float radius)
    {
        Rect query = new(p.X - radius, p.Y - radius, p.X + radius, p.Y + radius);
        HashSet<int>? seen = null;
        foreach ((int, int) cell in Cells(query))
        {
            if (_grid.TryGetValue(cell, out List<int>? list))
            {
                foreach (int i in list)
                {
                    seen ??= [];
                    if (seen.Add(i))
                    {
                        yield return _static[i];
                    }
                }
            }
        }

        foreach (Rect r in _dynamic)
        {
            if (r.Overlaps(query))
            {
                yield return r;
            }
        }
    }

    public bool Blocked(Vector2 p, float radius)
    {
        foreach (Rect r in Near(p, radius + 0.1f))
        {
            if (Vector2.DistanceSquared(r.Closest(p), p) < radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Moves a circle by <paramref name="delta"/>, pushing it out of
    /// obstacles so it slides along walls. Returns the new position.
    /// </summary>
    public Vector2 Move(Vector2 position, Vector2 delta, float radius)
    {
        float length = delta.Length();
        int steps = Math.Max(1, (int)MathF.Ceiling(length / (radius * 0.5f)));
        Vector2 step = delta / steps;
        Vector2 p = position;
        for (int s = 0; s < steps; s++)
        {
            p += step;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                bool pushed = false;
                foreach (Rect r in Near(p, radius + 0.2f))
                {
                    Vector2 closest = r.Closest(p);
                    Vector2 away = p - closest;
                    float d2 = away.LengthSquared();
                    if (d2 >= radius * radius)
                    {
                        continue;
                    }

                    if (d2 < 1e-8f)
                    {
                        // Centre inside the box: push out through the nearest side.
                        float left = p.X - r.X0;
                        float right = r.X1 - p.X;
                        float back = p.Y - r.Z0;
                        float front = r.Z1 - p.Y;
                        float min = MathF.Min(MathF.Min(left, right), MathF.Min(back, front));
                        p = min == left ? new Vector2(r.X0 - radius, p.Y)
                            : min == right ? new Vector2(r.X1 + radius, p.Y)
                            : min == back ? new Vector2(p.X, r.Z0 - radius)
                            : new Vector2(p.X, r.Z1 + radius);
                    }
                    else
                    {
                        float d = MathF.Sqrt(d2);
                        p = closest + (away / d * radius);
                    }

                    pushed = true;
                }

                if (!pushed)
                {
                    break;
                }
            }
        }

        // The upper floor lives outside the town bounds (see Floors).
        return Floors.IsUpper(p) ? Floors.UpperArea.Clamp(p, radius) : WorldMap.Bounds.Clamp(p, radius);
    }

    /// <summary>True when a straight walk from a to b is clear (sampled).</summary>
    public bool LineClear(Vector2 a, Vector2 b, float radius)
    {
        float length = Vector2.Distance(a, b);
        int samples = Math.Max(2, (int)(length / 0.2f));
        for (int i = 1; i <= samples; i++)
        {
            if (Blocked(Vector2.Lerp(a, b, i / (float)samples), radius))
            {
                return false;
            }
        }

        return true;
    }
}

using System.Numerics;

namespace OurHappyHome.Core.World;

/// <summary>
/// Occupancy grid with A* for autonomous characters. Built over a region
/// (the home lot, or a place the family visits) from the collision world.
/// </summary>
public sealed class NavGrid
{
    private readonly Rect _area;
    private readonly float _cell;
    private readonly int _width;
    private readonly int _height;
    private readonly bool[] _blocked;

    public NavGrid(Rect area, float cell, CollisionWorld collision, float radius)
    {
        _area = area;
        _cell = cell;
        _width = Math.Max(1, (int)MathF.Ceiling(area.Width / cell));
        _height = Math.Max(1, (int)MathF.Ceiling(area.Depth / cell));
        _blocked = new bool[_width * _height];
        for (int z = 0; z < _height; z++)
        {
            for (int x = 0; x < _width; x++)
            {
                _blocked[(z * _width) + x] = collision.Blocked(CellCenter(x, z), radius);
            }
        }
    }

    public Rect Area => _area;

    public bool Contains(Vector2 p) => _area.Contains(p);

    private Vector2 CellCenter(int x, int z) => new(_area.X0 + ((x + 0.5f) * _cell), _area.Z0 + ((z + 0.5f) * _cell));

    private (int X, int Z) ToCell(Vector2 p) =>
        (Math.Clamp((int)((p.X - _area.X0) / _cell), 0, _width - 1), Math.Clamp((int)((p.Y - _area.Z0) / _cell), 0, _height - 1));

    private bool Free(int x, int z) => x >= 0 && z >= 0 && x < _width && z < _height && !_blocked[(z * _width) + x];

    /// <summary>Nearest free cell to a point (furniture approach points can sit inside the inflated border).</summary>
    private (int X, int Z)? NearestFree((int X, int Z) c)
    {
        if (Free(c.X, c.Z))
        {
            return c;
        }

        for (int r = 1; r < 12; r++)
        {
            (int, int)? best = null;
            float bestD = float.MaxValue;
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r || !Free(c.X + dx, c.Z + dz))
                    {
                        continue;
                    }

                    float d = (dx * dx) + (dz * dz);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = (c.X + dx, c.Z + dz);
                    }
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        return null;
    }

    /// <summary>A* path from start to goal as a smoothed list of waypoints (goal included).</summary>
    public List<Vector2>? FindPath(Vector2 start, Vector2 goal, CollisionWorld? smoothing = null, float radius = 0.25f)
    {
        if (NearestFree(ToCell(start)) is not { } s || NearestFree(ToCell(goal)) is not { } g)
        {
            return null;
        }

        int startIndex = (s.Z * _width) + s.X;
        int goalIndex = (g.Z * _width) + g.X;
        Dictionary<int, int> cameFrom = [];
        Dictionary<int, float> cost = new() { [startIndex] = 0f };
        PriorityQueue<int, float> open = new();
        open.Enqueue(startIndex, 0f);
        int expanded = 0;

        (int dx, int dz, float c)[] moves =
        [
            (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
            (1, 1, 1.4142f), (1, -1, 1.4142f), (-1, 1, 1.4142f), (-1, -1, 1.4142f),
        ];

        bool found = false;
        while (open.Count > 0 && expanded < 40000)
        {
            int current = open.Dequeue();
            if (current == goalIndex)
            {
                found = true;
                break;
            }

            expanded++;
            int cx = current % _width;
            int cz = current / _width;
            foreach ((int dx, int dz, float c) in moves)
            {
                int nx = cx + dx;
                int nz = cz + dz;
                if (!Free(nx, nz))
                {
                    continue;
                }

                // No corner cutting through blocked cells.
                if (dx != 0 && dz != 0 && (!Free(cx + dx, cz) || !Free(cx, cz + dz)))
                {
                    continue;
                }

                int next = (nz * _width) + nx;
                float newCost = cost[current] + c;
                if (!cost.TryGetValue(next, out float old) || newCost < old)
                {
                    cost[next] = newCost;
                    cameFrom[next] = current;
                    float h = MathF.Sqrt(((nx - g.X) * (nx - g.X)) + ((nz - g.Z) * (nz - g.Z)));
                    open.Enqueue(next, newCost + h);
                }
            }
        }

        if (!found)
        {
            return null;
        }

        List<Vector2> cells = [];
        int node = goalIndex;
        while (node != startIndex)
        {
            cells.Add(CellCenter(node % _width, node / _width));
            node = cameFrom[node];
        }

        cells.Reverse();
        cells.Add(goal);

        if (smoothing is null)
        {
            return cells;
        }

        // String pulling: skip waypoints while the straight line stays clear.
        List<Vector2> smooth = [];
        Vector2 anchor = start;
        int i = 0;
        while (i < cells.Count)
        {
            int furthest = i;
            for (int j = cells.Count - 1; j > i; j--)
            {
                if (smoothing.LineClear(anchor, cells[j], radius * 0.9f))
                {
                    furthest = j;
                    break;
                }
            }

            smooth.Add(cells[furthest]);
            anchor = cells[furthest];
            i = furthest + 1;
        }

        return smooth;
    }
}

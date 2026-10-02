using System.Numerics;

namespace OurHappyHome.Core.World;

/// <summary>Axis aligned rectangle on the ground plane (X, Z).</summary>
public readonly record struct Rect(float X0, float Z0, float X1, float Z1)
{
    public static Rect FromCenter(Vector2 center, Vector2 size) =>
        new(center.X - (size.X / 2), center.Y - (size.Y / 2), center.X + (size.X / 2), center.Y + (size.Y / 2));

    public float Width => X1 - X0;

    public float Depth => Z1 - Z0;

    public Vector2 Center => new((X0 + X1) / 2, (Z0 + Z1) / 2);

    public Vector2 Size => new(Width, Depth);

    public bool Contains(Vector2 p) => p.X >= X0 && p.X <= X1 && p.Y >= Z0 && p.Y <= Z1;

    public bool Contains(Vector2 p, float margin) =>
        p.X >= X0 + margin && p.X <= X1 - margin && p.Y >= Z0 + margin && p.Y <= Z1 - margin;

    public bool Overlaps(Rect other) => X0 < other.X1 && X1 > other.X0 && Z0 < other.Z1 && Z1 > other.Z0;

    public Rect Inflate(float amount) => new(X0 - amount, Z0 - amount, X1 + amount, Z1 + amount);

    public Vector2 Clamp(Vector2 p, float margin = 0f) =>
        new(Math.Clamp(p.X, X0 + margin, X1 - margin), Math.Clamp(p.Y, Z0 + margin, Z1 - margin));

    /// <summary>Closest point of the rectangle to <paramref name="p"/>.</summary>
    public Vector2 Closest(Vector2 p) => new(Math.Clamp(p.X, X0, X1), Math.Clamp(p.Y, Z0, Z1));
}

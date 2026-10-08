using System.Numerics;

namespace OurHappyHome.Core.World;

/// <summary>
/// The house's upper floor. The simulation is 2D, so upstairs rooms live in
/// their own region of the plane (<see cref="UpperOffset"/> away from the lot)
/// with their own walls, collision and navigation; the renderer lifts that
/// region <see cref="Height"/> metres up and slides it back over the house.
/// The stairs in the hallway connect the two floors.
/// </summary>
public static class Floors
{
    /// <summary>Where the upstairs rooms sit on the simulation plane (far outside the town).</summary>
    public static readonly Vector2 UpperOffset = new(0f, -700f);

    /// <summary>Upper floor level above the ground floor.</summary>
    public const float Height = 2.85f;

    /// <summary>Everything upstairs (rooms and balcony) lies inside this rectangle.</summary>
    public static readonly Rect UpperArea = new(-8f, -708f, 8f, -692f);

    /// <summary>The staircase footprint in the ground-floor hallway (and the matching hole upstairs).</summary>
    public static readonly Rect Stairs = new(0.25f, -2.4f, 1.15f, -0.1f);

    public static Rect StairHole => Offset(Stairs);

    /// <summary>Where people stand to start climbing (ground floor, at the bottom step).</summary>
    public static readonly Vector2 StairBottom = new(0.7f, -2.8f);

    /// <summary>Where people step off at the top (upper floor, beside the stair hole).</summary>
    public static readonly Vector2 StairTop = new Vector2(-0.4f, -0.5f) + UpperOffset;

    /// <summary>Seconds a climb takes.</summary>
    public const float ClimbSeconds = 1.6f;

    public static bool IsUpper(Vector2 p) => UpperArea.Contains(p);

    public static Rect Offset(Rect r) => new(r.X0 + UpperOffset.X, r.Z0 + UpperOffset.Y, r.X1 + UpperOffset.X, r.Z1 + UpperOffset.Y);

    /// <summary>Position in the rendered world: upstairs points are lifted and moved over the house.</summary>
    public static Vector3 ToRender(Vector2 p, float height = 0f) =>
        IsUpper(p) ? new Vector3(p.X - UpperOffset.X, height + Height, p.Y - UpperOffset.Y) : new Vector3(p.X, height, p.Y);

    /// <summary>Same as <see cref="ToRender(Vector2, float)"/> for a point whose Y is already a height.</summary>
    public static Vector3 ToRender(Vector3 p) => ToRender(new Vector2(p.X, p.Z), p.Y);

    /// <summary>Rendered point along the staircase for a climb (0 = bottom step, 1 = top).</summary>
    public static Vector3 StairPoint(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        Vector3 bottom = new(0.7f, 0f, -2.55f);
        Vector3 top = new(0.7f, Height, -0.3f);
        return Vector3.Lerp(bottom, top, t);
    }
}

/// <summary>A member walking up or down the stairs (frozen in the simulation while it lasts).</summary>
public sealed class StairClimb
{
    public bool Up { get; set; }

    public float Time { get; set; }

    public float Progress => Math.Clamp(Time / Floors.ClimbSeconds, 0f, 1f);
}

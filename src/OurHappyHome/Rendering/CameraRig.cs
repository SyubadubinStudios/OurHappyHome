using System.Numerics;
using ThreeNet;

namespace OurHappyHome.Rendering;

public enum CameraMode
{
    Follow,
    Build,
    Showcase,
}

/// <summary>
/// Third-person orbit camera that follows the controlled family member. It
/// looks down at a steeper angle indoors (so the cut-away rooms read like a
/// doll's house) and lower outdoors; build mode orbits the whole house.
/// </summary>
public sealed class CameraRig
{
    private readonly Node _camera;
    private Vector3 _target;
    private float _shake;
    private float _time;
    private float _pitch = 0.6f;
    private float _distance = 10f;

    public CameraRig(Node camera)
    {
        _camera = camera;
    }

    public CameraMode Mode { get; set; } = CameraMode.Follow;

    /// <summary>Orbit angle around the target; 0 looks from the street (+Z) towards the house.</summary>
    public float Yaw { get; set; }

    /// <summary>Extra pitch from the player (mouse drag), added to the mode default.</summary>
    public float PitchOffset { get; set; }

    /// <summary>Zoom multiplier from the mouse wheel.</summary>
    public float Zoom { get; set; } = 1f;

    public Vector3 BuildCenter { get; set; } = new(0f, 0f, 0f);

    public Vector3 Position => _camera.Position;

    /// <summary>Horizontal forward direction for camera-relative movement (X, Z).</summary>
    public Vector2 Forward => new(-MathF.Sin(Yaw), -MathF.Cos(Yaw));

    public Vector2 Right => new(MathF.Cos(Yaw), -MathF.Sin(Yaw));

    public void Shake(float strength) => _shake = MathF.Max(_shake, strength);

    public void Snap(Vector3 focus)
    {
        _target = focus;
    }

    public void Update(float dt, Vector3 focus, bool indoors)
    {
        _time += dt;
        float basePitch;
        float baseDistance;
        Vector3 aim;
        switch (Mode)
        {
            case CameraMode.Build:
                basePitch = 1.05f;
                baseDistance = 30f;
                aim = BuildCenter;
                break;
            case CameraMode.Showcase:
                basePitch = 0.35f;
                baseDistance = 16f;
                aim = focus;
                Yaw += dt * 0.08f;
                break;
            default:
                basePitch = indoors ? 0.95f : 0.48f;
                baseDistance = indoors ? 12.5f : 9f;
                aim = focus + new Vector3(0f, 1.0f, 0f);
                break;
        }

        float targetPitch = Math.Clamp(basePitch + PitchOffset, 0.12f, 1.45f);
        float targetDistance = Math.Clamp(baseDistance * Zoom, 3f, 70f);
        float k = 1f - MathF.Exp(-6f * dt);
        _pitch = float.Lerp(_pitch, targetPitch, k);
        _distance = float.Lerp(_distance, targetDistance, k);
        _target = Vector3.Lerp(_target, aim, 1f - MathF.Exp(-10f * dt));

        Vector3 offset = new(MathF.Sin(Yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch), MathF.Cos(Yaw) * MathF.Cos(_pitch));
        Vector3 position = _target + (offset * _distance);
        if (_shake > 0.001f)
        {
            position += new Vector3(MathF.Sin(_time * 53f), MathF.Sin(_time * 41f), MathF.Cos(_time * 47f)) * _shake * 0.25f;
            _shake *= MathF.Exp(-4f * dt);
        }

        _camera.Position = position;
        _camera.LookAt(_target);
    }
}

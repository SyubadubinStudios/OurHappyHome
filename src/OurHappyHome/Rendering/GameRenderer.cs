using System.Numerics;
using OurHappyHome.Core;
using OurHappyHome.Core.Family;
using OurHappyHome.Core.Simulation;
using OurHappyHome.Core.World;
using ThreeNet;

namespace OurHappyHome.Rendering;

/// <summary>
/// Owns the ThreeNet scene and keeps it in step with the simulation: time of
/// day, weather (sky, fog, rain, wind, lightning), the house, the town, the
/// family and the particle effects requested by game events.
/// </summary>
public sealed class GameRenderer : IDisposable
{
    private readonly Node _sun;
    private readonly Node _fill;
    private float _flash;
    private float _time;
    private Node? _ghost;
    private Node? _ghostFootprint;
    private string? _ghostId;

    public GameRenderer(GameSession session)
    {
        Session = session;
        Scene = new Scene();
        Meshes = new Meshes(Scene);
        Textures = new Textures(Scene);
        Models = new ModelLibrary(Scene);
        Effects = new Effects(Scene, Meshes, Textures);
        Town = new TownView(Scene, Meshes, Textures, Models, Effects, session.Map);
        House = new HouseView(Scene, Meshes, Textures, Models, Effects);
        Characters = new CharacterView(Scene, Meshes, Textures, Models);

        _sun = Scene.AddLight(Light.Directional(Vector3.One, 3f) with { CastShadow = true, ShadowNormalBias = 1.6f }, name: "sun");
        _fill = Scene.AddLight(Light.Directional(new Vector3(0.5f, 0.6f, 0.8f), 0.4f), name: "sky-fill");
        CameraNode = Scene.AddCamera(Camera.Perspective(52f * MathF.PI / 180f, 0.1f, 700f), new Vector3(0, 8, 12));
        Rig = new CameraRig(CameraNode);
        Rig.Snap(new Vector3(session.Controlled.Position.X, 1f, session.Controlled.Position.Y));
    }

    public GameSession Session { get; set; }

    public Scene Scene { get; }

    public Meshes Meshes { get; }

    public Textures Textures { get; }

    public ModelLibrary Models { get; }

    public Effects Effects { get; }

    public TownView Town { get; }

    public HouseView House { get; }

    public CharacterView Characters { get; }

    public Node CameraNode { get; }

    public CameraRig Rig { get; }

    public bool ReducedIntensity { get; set; }

    public bool BuildMode
    {
        get => House.BuildMode;
        set
        {
            House.BuildMode = value;
            Rig.Mode = value ? CameraMode.Build : CameraMode.Follow;
            if (!value)
            {
                SetGhost(null, default, 0, true);
            }
        }
    }

    /// <summary>White-flash strength for the UI overlay (lightning).</summary>
    public float Flash => _flash;

    public void Update(float dt)
    {
        _time += dt;
        GameSession s = Session;
        FamilyMember player = s.Controlled;
        bool indoors = s.IsIndoors(player.Position);
        Vector3 focus = new(player.Position.X, player.Anchor?.Y ?? 0f, player.Position.Y);
        Rig.Update(dt, focus, indoors);

        ApplyTimeAndWeather(dt);
        Quaternion cameraRotation = CameraNode.Rotation;
        House.Sync(s, CameraNode.Position, dt);
        House.FaceIcons(cameraRotation);
        Characters.Update(s, dt, cameraRotation);
        Town.Update(dt, CameraNode.Position, s.Clock.IsNight, s.State.Weather.Current is WeatherKind.Windy or WeatherKind.SevereStorm ? s.State.Weather.Intensity : 0.1f);
        Effects.Update(dt, CameraNode);
        Scene.UpdateAnimations(dt);
        _flash = MathF.Max(0f, _flash - (dt * 3.5f));
    }

    public void HandleEvent(GameEvent e)
    {
        switch (e)
        {
            case EffectEvent effect:
                Effects.Burst(effect.Kind, effect.Position, effect.Scale);
                break;
            case ScreenFlashEvent flash when !ReducedIntensity:
                _flash = MathF.Max(_flash, flash.Strength);
                break;
            case ShakeEvent shake when !ReducedIntensity:
                Rig.Shake(shake.Strength);
                break;
            case ChapterEvent { Completed: true }:
                Effects.Burst(EffectKind.Fireworks, new Vector3(Session.Controlled.Position.X, 0f, Session.Controlled.Position.Y));
                break;
        }
    }

    // ------------------------------------------------------- time & weather

    private void ApplyTimeAndWeather(float dt)
    {
        float hour = Session.Hour;
        WeatherState weather = Session.State.Weather;

        float dayAngle = (hour - 6f) / 12f * MathF.PI;
        float elevation = MathF.Sin(dayAngle);
        float daylight = Math.Clamp((elevation + 0.08f) * 2.2f, 0f, 1f);
        float golden = Math.Clamp(1f - (MathF.Abs(elevation) * 3.2f), 0f, 1f) * daylight;

        float clouds = weather.Current switch
        {
            WeatherKind.Sunny => 0f,
            WeatherKind.Cloudy => 0.35f,
            WeatherKind.Rain => 0.55f,
            WeatherKind.Thunderstorm => 0.7f,
            WeatherKind.Fog => 0.45f,
            WeatherKind.Windy => 0.2f,
            _ => 0.8f,
        };

        Vector3 toSun = Vector3.Normalize(new Vector3(MathF.Cos(dayAngle), MathF.Max(elevation, 0.15f), 0.45f));
        Vector3 sunColour = Vector3.Lerp(new Vector3(1f, 0.96f, 0.88f), new Vector3(1f, 0.62f, 0.32f), golden);
        float sunPower = 3.3f * daylight * (1f - (clouds * 0.75f));
        if (daylight > 0.02f)
        {
            _sun.Light = Light.Directional(sunColour, sunPower) with { CastShadow = sunPower > 0.6f, ShadowNormalBias = 1.6f, ShadowStrength = 1f - (clouds * 0.6f) };
            _sun.Position = toSun * 120f;
        }
        else
        {
            _sun.Light = Light.Directional(new Vector3(0.5f, 0.6f, 0.95f), 0.4f * (1f - (clouds * 0.6f))) with { CastShadow = clouds < 0.5f, ShadowStrength = 0.5f };
            _sun.Position = new Vector3(-40f, 100f, 50f);
        }

        Vector3 focus = new(Session.Controlled.Position.X, 0f, Session.Controlled.Position.Y);
        _sun.Position += focus;
        _sun.LookAt(focus);

        Vector3 daySky = Vector3.Lerp(new Vector3(0.45f, 0.68f, 0.95f), new Vector3(0.55f, 0.6f, 0.66f), clouds);
        Vector3 duskSky = new(0.95f, 0.55f, 0.38f);
        Vector3 nightSky = Vector3.Lerp(new Vector3(0.02f, 0.03f, 0.08f), new Vector3(0.03f, 0.03f, 0.05f), clouds);
        Vector3 sky = Vector3.Lerp(Vector3.Lerp(nightSky, daySky, daylight), duskSky, golden * 0.65f * (1f - clouds));

        Vector3 ambient = Vector3.Lerp(new Vector3(0.18f, 0.22f, 0.4f), new Vector3(0.62f, 0.68f, 0.8f), daylight);
        float ambientIntensity = 0.16f + (daylight * 0.24f);
        if (_flash > 0f)
        {
            sky = Vector3.Lerp(sky, new Vector3(0.85f, 0.88f, 1f), _flash * 0.8f);
            ambientIntensity += _flash * 1.5f;
        }

        float fog = 0.0025f + (clouds * 0.004f) + (weather.Current == WeatherKind.Fog ? weather.Intensity * 0.035f : 0f) + (weather.IsRaining ? 0.006f : 0f);
        Scene.Environment = Scene.Environment with
        {
            Background = new Vector4(sky, 1f),
            AmbientColor = ambient,
            AmbientIntensity = ambientIntensity,
            FogColor = Vector3.Lerp(sky, new Vector3(0.7f, 0.72f, 0.75f), weather.Current == WeatherKind.Fog ? 0.5f : 0.1f),
            FogDensity = fog,
        };

        _fill.Light = Light.Directional(Vector3.Lerp(new Vector3(0.3f, 0.35f, 0.6f), new Vector3(0.6f, 0.68f, 0.85f), daylight), 0.25f + (daylight * 0.3f));
        _fill.LookAt(new Vector3(0.3f, -1f, -0.4f));

        bool outside = !Session.IsIndoors(Session.Controlled.Position);
        Effects.RainIntensity = weather.IsRaining && Session.CurrentInterior is null ? weather.Intensity * (outside ? 1f : 0.55f) : 0f;
        Effects.Wind = weather.Current switch
        {
            WeatherKind.Windy => 2.5f * weather.Intensity,
            WeatherKind.SevereStorm => 4f,
            WeatherKind.Thunderstorm => 1.5f,
            _ => 0.2f,
        };
        if (weather.Current is WeatherKind.Windy or WeatherKind.SevereStorm && Random.Shared.NextDouble() < dt * 2.0)
        {
            Effects.Burst(EffectKind.Leaves, CameraNode.Position + new Vector3(Random.Shared.Next(-6, 6), -3f, Random.Shared.Next(-10, -2)), 0.6f);
        }
    }

    // ------------------------------------------------------------- picking

    /// <summary>Ground point under a screen position (normalised device coordinates).</summary>
    public Vector2? GroundAt(float ndcX, float ndcY, float aspect)
    {
        Ray ray = Scene.CreateCameraRay(CameraNode, ndcX, ndcY, aspect);
        if (MathF.Abs(ray.Direction.Y) < 1e-4f)
        {
            return null;
        }

        float t = -ray.Origin.Y / ray.Direction.Y;
        if (t < 0f)
        {
            return null;
        }

        Vector3 hit = ray.At(t);
        return new Vector2(hit.X, hit.Z);
    }

    /// <summary>Projects a world point to normalised screen coordinates (0-1); null when behind the camera.</summary>
    public Vector2? ScreenPoint(Vector3 world, float aspect)
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(CameraNode.Position, CameraNode.Position + Vector3.Transform(-Vector3.UnitZ, CameraNode.Rotation), Vector3.Transform(Vector3.UnitY, CameraNode.Rotation));
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(52f * MathF.PI / 180f, aspect, 0.1f, 700f);
        Vector4 clip = Vector4.Transform(new Vector4(world, 1f), view * projection);
        if (clip.W <= 0.01f)
        {
            return null;
        }

        return new Vector2((clip.X / clip.W * 0.5f) + 0.5f, 0.5f - (clip.Y / clip.W * 0.5f));
    }

    // --------------------------------------------------------- build ghost

    public void SetGhost(FurnitureDef? def, Vector2 position, int rotation, bool valid)
    {
        if (def is null)
        {
            _ghost?.Remove();
            _ghostFootprint?.Remove();
            _ghost = null;
            _ghostFootprint = null;
            _ghostId = null;
            return;
        }

        if (_ghostId != def.Id || _ghost is null)
        {
            _ghost?.Remove();
            _ghostFootprint?.Remove();
            _ghost = Scene.CreateNode(null, "ghost");
            FurnitureFactory.Build(def, _ghost, Meshes, Textures, Models, 1);
            _ghost.SetShadowsRecursive(false, false);
            _ghostFootprint = Scene.AddMesh(Meshes.UnitBox, Textures.Solid("#7CFF9B", 0.5f, emissive: 1f), null, "ghost-footprint");
            _ghostId = def.Id;
        }

        float yaw = rotation * MathF.PI / 2f;
        _ghost.Position = new Vector3(position.X, 0.02f + (0.05f * MathF.Sin(_time * 6f)), position.Y);
        _ghost.EulerAngles = new Vector3(0f, yaw, 0f);
        Vector2 size = rotation % 2 == 1 ? new Vector2(def.Footprint.Y, def.Footprint.X) : def.Footprint;
        _ghostFootprint!.Position = new Vector3(position.X, 0.03f, position.Y);
        _ghostFootprint.Scale = new Vector3(size.X, 0.02f, size.Y);
        _ghostFootprint.DetachMesh();
        _ghostFootprint.AttachMesh(Meshes.UnitBox, Textures.Solid(valid ? "#7CFF9B" : "#FF5C5C", 0.5f, emissive: 1.2f));
    }

    public void Dispose() => Scene.Dispose();
}

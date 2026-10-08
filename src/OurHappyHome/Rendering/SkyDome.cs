using System.Numerics;
using Avalonia;
using Avalonia.Media;
using ThreeNet;
using Geometry = ThreeNet.Geometry;

namespace OurHappyHome.Rendering;

/// <summary>
/// The sky: ThreeNet has no skybox, so two large inside-out spheres follow the
/// camera. The inner one is a vertical gradient tinted with the time-of-day sky
/// colour; the outer one is a painted band of soft clouds that drifts slowly and
/// changes colour at dusk, at night and in bad weather.
/// </summary>
public sealed class SkyDome
{
    private const float Radius = 600f;
    private readonly Node _sky;
    private readonly Node _clouds;
    private readonly Material _skyMaterial;
    private readonly Material _cloudMaterial;
    private readonly Node _stars;
    private readonly Material _starMaterial;
    private float _yaw;

    public SkyDome(Scene scene, Textures textures)
    {
        Geometry sphere = scene.CreateSphereGeometry(1f, 48, 24);

        Texture gradient = textures.Draw(16, 512, dc =>
        {
            LinearGradientBrush brush = new()
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(150, 185, 255), 0.0),
                    new GradientStop(Color.FromRgb(205, 225, 255), 0.32),
                    new GradientStop(Color.FromRgb(250, 252, 255), 0.49),
                    new GradientStop(Color.FromRgb(255, 255, 255), 0.5),
                    new GradientStop(Color.FromRgb(250, 252, 255), 0.51),
                    new GradientStop(Color.FromRgb(205, 225, 255), 0.68),
                    new GradientStop(Color.FromRgb(150, 185, 255), 1.0),
                },
            };
            dc.FillRectangle(brush, new Rect(0, 0, 16, 512));
        });
        _skyMaterial = scene.CreateMaterial(MaterialOptions.Basic(Vector4.One) with
        {
            BaseColorMap = gradient,
            CullMode = CullMode.None,
        });
        _sky = scene.AddMesh(sphere, _skyMaterial, null, "sky");
        _sky.CastShadow = false;
        _sky.ReceiveShadow = false;
        _sky.Scale = new Vector3(Radius);

        Texture clouds = textures.Draw(1024, 512, PaintClouds);
        _cloudMaterial = scene.CreateMaterial(MaterialOptions.Basic(Vector4.One) with
        {
            BaseColorMap = clouds,
            AlphaMode = AlphaMode.Blend,
            CullMode = CullMode.None,
            DepthWrite = false,
            RenderOrder = -20,
        });
        _clouds = scene.AddMesh(sphere, _cloudMaterial, null, "clouds");
        _clouds.CastShadow = false;
        _clouds.ReceiveShadow = false;
        _clouds.Scale = new Vector3(Radius * 0.96f);

        Texture stars = textures.Draw(2048, 512, PaintStars);
        _starMaterial = scene.CreateMaterial(MaterialOptions.Basic(Vector4.One) with
        {
            BaseColorMap = stars,
            AlphaMode = AlphaMode.Blend,
            CullMode = CullMode.None,
            DepthWrite = false,
            RenderOrder = -30,
        });
        _stars = scene.AddMesh(sphere, _starMaterial, null, "stars");
        _stars.CastShadow = false;
        _stars.ReceiveShadow = false;
        _stars.Scale = new Vector3(Radius * 0.98f);
    }

    /// <summary>
    /// Soft cumulus puffs. The game camera looks slightly down, so most clouds sit
    /// low over the horizon (v just under 0.5), with a few larger ones higher up.
    /// </summary>
    private static void PaintClouds(DrawingContext dc)
    {
        Random r = new(23);
        for (int i = 0; i < 150; i++)
        {
            double cx = r.NextDouble() * 1024;
            bool high = i % 5 == 0;
            double band = r.NextDouble();
            double cy = high ? 120 + (band * 80) : 222 + (band * 30); // 256 is the horizon
            double scale = high ? 1.3 : 0.55 + ((1 - band) * 0.5);
            int puffs = 3 + r.Next(4);
            for (int p = 0; p < puffs; p++)
            {
                double w = (34 + (r.NextDouble() * 40)) * scale;
                double h = w * (high ? 0.45 : 0.32);
                double x = cx + ((p - (puffs / 2.0)) * w * 0.5);
                double y = cy - (r.NextDouble() * h * 0.5);
                RadialGradientBrush brush = new()
                {
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(250, 255, 255, 255), 0),
                        new GradientStop(Color.FromArgb(215, 252, 252, 255), 0.5),
                        new GradientStop(Color.FromArgb(0, 245, 246, 250), 1),
                    },
                };
                // Mirrored about the horizon line, so it does not matter which way the sphere's V runs.
                foreach (double wrap in new[] { 0.0, -1024.0, 1024.0 })
                {
                    dc.DrawEllipse(brush, null, new Rect(x + wrap - (w / 2), y - (h / 2), w, h));
                    dc.DrawEllipse(brush, null, new Rect(x + wrap - (w / 2), 512 - y - (h / 2), w, h));
                }
            }
        }
    }

    /// <summary>Little white stars across the upper sky, a few twinkling brighter ones.</summary>
    private static void PaintStars(DrawingContext dc)
    {
        Random r = new(91);
        for (int i = 0; i < 900; i++)
        {
            double x = r.NextDouble() * 2048;
            double y = r.NextDouble() * 250;
            double size = r.NextDouble() < 0.08 ? 2.6 : 1.3;
            byte a = (byte)(120 + r.Next(135));
            SolidColorBrush star = new(Color.FromArgb(a, 255, 255, (byte)(225 + r.Next(30))));
            dc.DrawEllipse(star, null, new Rect(x, y, size, size));
            dc.DrawEllipse(star, null, new Rect(x, 512 - y, size, size));
        }
    }

    /// <summary>Follows the camera; <paramref name="sky"/> tints the gradient, the clouds get their own tint and opacity.</summary>
    public void Update(float dt, Vector3 cameraPosition, Vector3 sky, Vector3 cloudTint, float cloudOpacity, float windSpeed, float starOpacity)
    {
        _sky.Position = cameraPosition;
        _stars.Position = cameraPosition;
        _stars.EulerAngles = new Vector3(0f, _yaw * 0.3f, 0f);
        _starMaterial.Update(o => o with { BaseColor = new Vector4(1f, 1f, 1f, Math.Clamp(starOpacity, 0f, 1f)) });
        _clouds.Position = cameraPosition;
        _yaw += dt * (0.004f + (windSpeed * 0.01f));
        _clouds.EulerAngles = new Vector3(0f, _yaw, 0f);
        _skyMaterial.Update(o => o with { BaseColor = new Vector4(sky, 1f) });
        _cloudMaterial.Update(o => o with { BaseColor = new Vector4(cloudTint, Math.Clamp(cloudOpacity, 0f, 1f)) });
    }
}

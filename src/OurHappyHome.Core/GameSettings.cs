namespace OurHappyHome.Core;

public enum ColorAssist
{
    None,
    Deuteranopia,
    Protanopia,
    Tritanopia,
    HighContrast,
}

public enum GraphicsQuality
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Player preferences, including the accessibility options from design
/// section 27 (difficulty, simplified controls, reading assistance,
/// subtitles, colour settings, reduced emergency intensity, tutorials).
/// Stored separately from saves.
/// </summary>
public sealed class GameSettings
{
    public Language Language { get; set; } = Language.Indonesian;

    public float MasterVolume { get; set; } = 0.9f;
    public float MusicVolume { get; set; } = 0.55f;
    public float SfxVolume { get; set; } = 0.8f;
    public float VoiceVolume { get; set; } = 1.0f;

    /// <summary>UI text scale, 1.0 = normal (reading assistance raises it).</summary>
    public float TextScale { get; set; } = 1.0f;

    public bool Subtitles { get; set; } = true;

    /// <summary>Reads objectives and dialogue aloud where voice clips exist, and shows icons next to every text.</summary>
    public bool ReadingAssist { get; set; }

    public ColorAssist ColorAssist { get; set; } = ColorAssist.None;

    /// <summary>No screen shake or flashes, calmer emergency music, longer rescue timers.</summary>
    public bool ReducedIntensity { get; set; }

    /// <summary>Click to move and one-button interactions.</summary>
    public bool SimplifiedControls { get; set; }

    public bool Tutorials { get; set; } = true;

    public GraphicsQuality Quality { get; set; } = GraphicsQuality.High;

    public bool ShowFps { get; set; }

    public bool Fullscreen { get; set; }

    /// <summary>Rescue timer multiplier chosen as "difficulty" (Easy 1.6, Normal 1.0, Hard 0.75).</summary>
    public float RescueTimeScale { get; set; } = 1.0f;

    public static string FilePath => Path.Combine(SaveSystem.Folder, "settings.json");

    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return System.Text.Json.JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath)) ?? new GameSettings();
            }
        }
        catch (Exception)
        {
            // A broken settings file falls back to defaults.
        }

        return new GameSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SaveSystem.Folder);
        File.WriteAllText(FilePath, System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}

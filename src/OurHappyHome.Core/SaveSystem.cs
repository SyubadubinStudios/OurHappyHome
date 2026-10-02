using System.Text.Json;
using System.Text.Json.Serialization;

namespace OurHappyHome.Core;

public sealed record SaveInfo(string Slot, string Path, string Name, DateTime SavedAt, int Day, string House, long Money, int Chapter);

/// <summary>JSON saves in %AppData%/OurHappyHome (photos for the album live next to them).</summary>
public static class SaveSystem
{
    private static string? _folder;

    public static string Folder
    {
        get => _folder ??= System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OurHappyHome");
        set => _folder = value;
    }

    public static string PhotoFolder => System.IO.Path.Combine(Folder, "photos");

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        IgnoreReadOnlyProperties = true,
        IncludeFields = false,
        Converters = { new JsonStringEnumConverter(), new Vector2Converter() },
    };

    public static string Serialize(GameState state)
    {
        state.BeforeSave();
        return JsonSerializer.Serialize(state, Options);
    }

    public static GameState Deserialize(string json)
    {
        GameState state = JsonSerializer.Deserialize<GameState>(json, Options) ?? throw new InvalidDataException("Empty save");
        state.AfterLoad();
        return state;
    }

    public static string SlotPath(string slot) => System.IO.Path.Combine(Folder, $"{slot}.ohh.json");

    public static void Save(GameState state, string slot)
    {
        Directory.CreateDirectory(Folder);
        string path = SlotPath(slot);
        string temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(state));
        File.Move(temp, path, overwrite: true);
    }

    public static GameState Load(string slot) => Deserialize(File.ReadAllText(SlotPath(slot)));

    public static bool Exists(string slot) => File.Exists(SlotPath(slot));

    public static IReadOnlyList<SaveInfo> List()
    {
        List<SaveInfo> saves = [];
        if (!Directory.Exists(Folder))
        {
            return saves;
        }

        foreach (string file in Directory.GetFiles(Folder, "*.ohh.json"))
        {
            try
            {
                GameState state = Deserialize(File.ReadAllText(file));
                string slot = System.IO.Path.GetFileName(file)[..^".ohh.json".Length];
                saves.Add(new SaveInfo(slot, file, state.SaveName, state.SavedAt, state.Clock.DayIndex + 1, state.House.Title, state.Wallet.Money, state.Chapter));
            }
            catch (Exception)
            {
                // Skip unreadable files.
            }
        }

        return [.. saves.OrderByDescending(s => s.SavedAt)];
    }

    public static void Delete(string slot)
    {
        if (Exists(slot))
        {
            File.Delete(SlotPath(slot));
        }
    }

    private sealed class Vector2Converter : JsonConverter<System.Numerics.Vector2>
    {
        public override System.Numerics.Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            float[] values = JsonSerializer.Deserialize<float[]>(ref reader) ?? [0, 0];
            return new System.Numerics.Vector2(values.Length > 0 ? values[0] : 0, values.Length > 1 ? values[1] : 0);
        }

        public override void Write(Utf8JsonWriter writer, System.Numerics.Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteEndArray();
        }
    }
}

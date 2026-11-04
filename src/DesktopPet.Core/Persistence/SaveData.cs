using System.Text.Json;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Persistence;

/// <summary>What survives a restart. Versioned for future migrations.</summary>
public sealed record SaveData
{
    /// <summary>2 added <see cref="LastMatedUtc"/>; version 1 files load with it unset.</summary>
    public const int CurrentVersion = 2;

    public int Version { get; init; } = CurrentVersion;
    public double Hunger { get; init; }
    public double Happiness { get; init; }
    public double Energy { get; init; }
    public DateTimeOffset SavedAtUtc { get; init; }

    /// <summary>When it last mated or hatched (the mating cooldown runs from here); null if never.</summary>
    public DateTimeOffset? LastMatedUtc { get; init; }

    public PetStats Stats => new PetStats(Hunger, Happiness, Energy).Clamped();

    public static SaveData From(PetStats stats, DateTimeOffset now, DateTimeOffset? lastMated = null) =>
        new() { Hunger = stats.Hunger, Happiness = stats.Happiness, Energy = stats.Energy, SavedAtUtc = now, LastMatedUtc = lastMated };
}

public interface ISaveStore
{
    /// <summary>The saved pet, or null if there is none or it can't be read.</summary>
    SaveData? Load();

    void Save(SaveData data);
}

/// <summary>
/// JSON file store with atomic replace: writes <c>save.json.tmp</c> then swaps it in, so a crash mid-write
/// never leaves a half-written save. A corrupt or unreadable file loads as "no save" (fresh pet) rather than crashing.
/// </summary>
public sealed class JsonFileSaveStore(string path) : ISaveStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Path { get; } = path;

    public SaveData? Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return null;
            }

            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(Path), Options);
            return data is { Version: >= 1 } ? data : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Options));
        File.Move(temp, Path, overwrite: true);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopPet.Core.Death;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Persistence;

/// <summary>What survives a restart. Versioned for future migrations.</summary>
public sealed record SaveData
{
    /// <summary>
    /// 2 added <see cref="LastMatedUtc"/>; 3 added <see cref="BornUtc"/>, <see cref="Neglect"/> and the death fields.
    /// Older files load with them unset.
    /// </summary>
    public const int CurrentVersion = 3;

    public int Version { get; init; } = CurrentVersion;
    public double Hunger { get; init; }
    public double Happiness { get; init; }
    public double Energy { get; init; }
    public DateTimeOffset SavedAtUtc { get; init; }

    /// <summary>When it last mated or hatched (the mating cooldown runs from here); null if never.</summary>
    public DateTimeOffset? LastMatedUtc { get; init; }

    /// <summary>When it was born or added; null if unknown (pets from before version 3).</summary>
    public DateTimeOffset? BornUtc { get; init; }

    /// <summary>How long its needs have been neglected (the countdowns to death); null for none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NeglectTimers? Neglect { get; init; }

    /// <summary>When it died; null while it's alive. A dead pet isn't started: it has a gravestone instead.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? DiedUtc { get; init; }

    /// <summary>What it died of (with <see cref="DiedUtc"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter<DeathCause>))]
    public DeathCause? DiedOf { get; init; }

    [JsonIgnore]
    public bool IsDead => DiedUtc is not null;

    public PetStats Stats => new PetStats(Hunger, Happiness, Energy).Clamped();

    public static SaveData From(PetStats stats, DateTimeOffset now, DateTimeOffset? lastMated = null) =>
        new() { Hunger = stats.Hunger, Happiness = stats.Happiness, Energy = stats.Energy, SavedAtUtc = now, LastMatedUtc = lastMated };

    /// <summary>
    /// The save of a pet brought back to life: alive, every need at <see cref="DeathRules.ReviveStat"/>, no neglect;
    /// it keeps its birth date and mating cooldown.
    /// </summary>
    public SaveData Revived(DeathRules rules, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var stat = rules.ReviveStat;
        return this with
        {
            Version = CurrentVersion, Hunger = stat, Happiness = stat, Energy = stat, SavedAtUtc = now,
            Neglect = null, DiedUtc = null, DiedOf = null,
        };
    }
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

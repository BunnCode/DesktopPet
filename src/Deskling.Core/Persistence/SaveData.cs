using System.Text.Json.Serialization;
using Deskling.Core.Death;
using Deskling.Core.Stats;
using Deskling.Core.Traits;

namespace Deskling.Core.Persistence;

/// <summary>
/// What survives a restart: a pet's <c>save.json</c> (<see cref="PetSaveFile"/>). Versioned for future migrations.
/// </summary>
public sealed record SaveData
{
    /// <summary>
    /// 2 added <see cref="LastMatedUtc"/>; 3 added <see cref="BornUtc"/>, <see cref="Neglect"/> and the death fields;
    /// 4 added <see cref="Traits"/> (they were in traits.json). Older files load with them unset.
    /// </summary>
    public const int CurrentVersion = 4;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// The pet's name and multipliers, the hand-editable part of the file; null if it has none or they aren't valid.
    /// Read and written by <see cref="PetSaveFile"/> as its own section, so broken traits don't lose the stats.
    /// </summary>
    [JsonIgnore]
    public PetTraits? Traits { get; init; }
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

    [JsonIgnore]
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
            Version = CurrentVersion,
            Hunger = stat,
            Happiness = stat,
            Energy = stat,
            SavedAtUtc = now,
            Neglect = null,
            DiedUtc = null,
            DiedOf = null,
        };
    }
}

public interface ISaveStore
{
    /// <summary>The saved pet, or null if there is none or it can't be read.</summary>
    SaveData? Load();

    /// <summary>
    /// Writes <paramref name="data"/>. The traits already in the file are kept as they are (the user may have edited
    /// them), unless <paramref name="replaceTraits"/> or the file has none; then <see cref="SaveData.Traits"/> is written.
    /// </summary>
    void Save(SaveData data, bool replaceTraits = false);
}

/// <summary>
/// JSON file store with atomic replace: writes <c>save.json.tmp</c> then swaps it in, so a crash mid-write
/// never leaves a half-written save. A corrupt or unreadable file loads as "no save" (fresh pet) rather than crashing.
/// Saving over a file that isn't valid JSON (a hand edit gone wrong) first copies it to <see cref="BrokenCopyPath"/>.
/// </summary>
public sealed class JsonFileSaveStore(string path) : ISaveStore
{
    public string Path { get; } = path;

    /// <summary>Where an unreadable file is kept before it's saved over: <c>save.bad.json</c> next to it.</summary>
    public string BrokenCopyPath => System.IO.Path.ChangeExtension(Path, ".bad.json");

    /// <summary>A file that wasn't valid JSON was copied to <see cref="BrokenCopyPath"/> before being saved over.</summary>
    public event EventHandler<string>? BrokenFileKept;

    public SaveData? Load() => LoadFile()?.Save;

    /// <summary>The whole file as parsed, or null if there is none or it can't be read right now.</summary>
    public PetFileParseResult? LoadFile()
    {
        try
        {
            return File.Exists(Path) ? PetSaveFile.Parse(File.ReadAllText(Path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(SaveData data, bool replaceTraits = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var onDisk = File.Exists(Path) ? File.ReadAllText(Path) : null;
        var (text, broken) = PetSaveFile.Merge(onDisk, data, replaceTraits);
        if (broken)
        {
            File.Copy(Path, BrokenCopyPath, overwrite: true);
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, Path, overwrite: true);
        if (broken)
        {
            BrokenFileKept?.Invoke(this, BrokenCopyPath);
        }
    }
}

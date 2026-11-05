using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Deskling.Core.Stats;
using Deskling.Core.Traits;

namespace Deskling.Core.Persistence;

/// <summary>
/// The outcome of reading a pet's <c>save.json</c>. <paramref name="Save"/>: the pet's stats and dates (with its traits
/// when they're valid), or null when the file isn't usable. <paramref name="Traits"/>: its <c>Traits</c> section, or
/// null when the file has none; also the reason when the file isn't valid JSON.
/// </summary>
public sealed record PetFileParseResult(SaveData? Save, TraitsParseResult? Traits);

/// <summary>
/// A pet's <c>save.json</c>: its stats and dates, which the app rewrites every minute, plus its <c>Traits</c> (name
/// and multipliers), which the user may edit while the app runs. Forgiving like <c>settings.json</c> (comments,
/// trailing commas, any key casing). The traits are read on their own, so a mistake there doesn't lose the stats, and
/// <see cref="Merge"/> keeps them as the user left them when the app saves.
/// </summary>
public static class PetSaveFile
{
    public const string TraitsKey = "Traits";

    public const string Header =
        "// This pet's save. Edit Traits and save the file: changes apply right away.\n" +
        "//   Neediness: how fast each need drops (1 = average, 2 = twice as fast, 0.5 = half as fast).\n" +
        "//   Behavior: how often it idles, walks, sits, naps, climbs walls and pushes windows (1 = average).\n" +
        "//   Chat: how often it complains about its needs, chats with other pets, tells jokes, leaves sticky notes,\n" +
        "//   and (Curiosity) talks or writes about what it reads on screen.\n" +
        "//   Multipliers can be 0 to 10. Remove a line to set it back to 1.\n" +
        "// Everything below Traits is the app's own: it's rewritten every minute, so change it only while the app is closed.\n";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // a local file people read: "+00:00" and "Zoë" as they are
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The file for <paramref name="data"/>, with its <see cref="SaveData.Traits"/> (if any).</summary>
    public static string Serialize(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Serialize(data, data.Traits is { } traits ? PetTraitsFile.ToNode(traits) : null);
    }

    public static PetFileParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryParseObject(text, out var root, out var error))
        {
            return new PetFileParseResult(null, new TraitsParseResult(null, [error]));
        }

        var traitsNode = Detach(root, out var hasTraits);
        var traits = hasTraits ? PetTraitsFile.Parse(traitsNode) : null;
        SaveData? save;
        try
        {
            save = root.Deserialize<SaveData>(Options);
        }
        catch (JsonException)
        {
            save = null;
        }

        save = save is { Version: >= 1 } ? save with { Traits = traits?.Traits } : null;
        return new PetFileParseResult(save, traits);
    }

    /// <summary>
    /// What to write when saving <paramref name="data"/> over <paramref name="onDisk"/> (null: no file). The file's
    /// own <c>Traits</c> section is kept exactly as it is (the user may be editing it, maybe with a mistake still in
    /// it), unless <paramref name="replaceTraits"/> or it has none. <c>Broken</c>: the file on disk isn't valid JSON,
    /// so the new text doesn't keep anything from it; keep a copy of it first.
    /// </summary>
    public static (string Text, bool Broken) Merge(string? onDisk, SaveData data, bool replaceTraits)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (onDisk is null || replaceTraits)
        {
            return (Serialize(data), false);
        }

        if (!TryParseObject(onDisk, out var root, out _))
        {
            return (Serialize(data), true);
        }

        var kept = Detach(root, out var hasTraits);
        return hasTraits && kept is not null ? (Serialize(data, kept), false) : (Serialize(data), false);
    }

    /// <summary>
    /// A save with a pre-version-4 <c>traits.json</c> folded in as its <c>Traits</c> section (as written, mistakes and
    /// all). Without a usable save, the pet starts fresh at <paramref name="now"/>. Null when either file isn't valid
    /// JSON, with the reason.
    /// </summary>
    public static (string? Text, string? Error) FoldIn(string? saveText, string traitsText, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(traitsText);
        if (!TryParseNode(traitsText, out var traits, out var traitsError))
        {
            return (null, $"{PetTraitsFile.LegacyFileName}: {traitsError}");
        }

        SaveData? save = null;
        if (saveText is not null)
        {
            if (!TryParseObject(saveText, out var root, out var saveError))
            {
                return (null, $"{PetRoster.SaveFileName}: {saveError}");
            }

            Detach(root, out _); // traits.json wins over a Traits section added by hand
            try
            {
                save = root.Deserialize<SaveData>(Options);
            }
            catch (JsonException)
            {
                save = null;
            }
        }

        save = save is { Version: >= 1 } ? save with { Version = SaveData.CurrentVersion } : SaveData.From(PetStats.Fresh, now);
        return (Serialize(save, traits), null);
    }

    private static string Serialize(SaveData data, JsonNode? traits)
    {
        var fields = JsonSerializer.SerializeToNode(data, Options)!.AsObject();
        var file = new JsonObject { [nameof(SaveData.Version)] = data.Version };
        if (traits is not null)
        {
            file[TraitsKey] = traits;
        }

        foreach (var (key, value) in fields.ToList())
        {
            if (key != nameof(SaveData.Version))
            {
                fields.Remove(key);
                file[key] = value;
            }
        }

        return Header + file.ToJsonString(Options) + "\n";
    }

    /// <summary>Removes the <c>Traits</c> property (any casing) from <paramref name="root"/> and returns its value.</summary>
    private static JsonNode? Detach(JsonObject root, out bool found)
    {
        var key = root.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, TraitsKey, StringComparison.OrdinalIgnoreCase));
        found = key is not null;
        if (key is null)
        {
            return null;
        }

        var node = root[key];
        root.Remove(key);
        return node;
    }

    private static bool TryParseObject(string text, out JsonObject root, out string error)
    {
        root = null!;
        if (!TryParseNode(text, out var node, out error))
        {
            return false;
        }

        if (node is not JsonObject obj)
        {
            error = "The file is empty.";
            return false;
        }

        root = obj;
        return true;
    }

    private static bool TryParseNode(string text, out JsonNode? node, out string error)
    {
        try
        {
            node = JsonNode.Parse(text, new JsonNodeOptions { PropertyNameCaseInsensitive = true }, DocumentOptions);
            error = string.Empty;
            return true;
        }
        catch (JsonException e)
        {
            node = null;
            error = e.LineNumber is { } line ? $"Not valid JSON (line {line + 1})." : "Not valid JSON.";
            return false;
        }
    }
}

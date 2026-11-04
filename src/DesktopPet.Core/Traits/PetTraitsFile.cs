using System.Globalization;
using System.Text.Json;

namespace DesktopPet.Core.Traits;

/// <summary>The outcome of reading a pet's traits: usable traits, or the reasons they can't be used.</summary>
public sealed record TraitsParseResult(PetTraits? Traits, IReadOnlyList<string> Errors);

/// <summary>
/// A pet's <c>traits.json</c>: its name and multipliers. Forgiving to hand-edit like <c>settings.json</c> (comments,
/// trailing commas, any key casing, missing keys default to 1) and strict about values.
/// </summary>
public static class PetTraitsFile
{
    public const string FileName = "traits.json";

    /// <summary>Longest allowed name, in characters.</summary>
    public const int MaxNameLength = 24;

    /// <summary>Largest allowed multiplier.</summary>
    public const double MaxMultiplier = 10;

    public const string Header =
        "// This pet's traits. Edit and save: changes apply right away.\n" +
        "// Neediness: how fast each need drops (1 = average, 2 = twice as fast, 0.5 = half as fast).\n" +
        "// Behavior: how often it idles, walks, sits, naps, climbs walls and pushes windows (1 = average).\n" +
        "// Chat: how often it complains about its needs, chats with other pets, tells jokes, leaves sticky notes,\n" +
        "// and (Curiosity) talks or writes about what it reads on screen.\n" +
        "// Multipliers can be 0 to 10. Remove a line to set it back to 1.\n";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(PetTraits traits) => Header + JsonSerializer.Serialize(traits, Options) + "\n";

    public static TraitsParseResult Parse(string text)
    {
        PetTraits? traits;
        try
        {
            traits = JsonSerializer.Deserialize<PetTraits>(text, Options);
        }
        catch (JsonException e)
        {
            var where = e.LineNumber is { } line ? $" (line {line + 1})" : string.Empty;
            return new TraitsParseResult(null, [$"Not valid JSON{where}."]);
        }

        if (traits is null)
        {
            return new TraitsParseResult(null, ["The file is empty."]);
        }

        traits = traits with
        {
            Name = traits.Name?.Trim() ?? string.Empty,
            Neediness = traits.Neediness ?? new Neediness(),
            Behavior = traits.Behavior ?? new BehaviorTraits(),
            Chat = traits.Chat ?? new ChatTraits(),
            Jacket = string.IsNullOrWhiteSpace(traits.Jacket) ? null : traits.Jacket.Trim(),
            Parents = traits.Parents is { Count: > 0 } parents ? new Chat.TextList(parents.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())) : null,
        };
        var errors = Validate(traits);
        return new TraitsParseResult(errors.Count == 0 ? traits : null, errors);
    }

    public static IReadOnlyList<string> Validate(PetTraits traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        var errors = new List<string>();

        void Range(string name, double value)
        {
            if (!double.IsFinite(value) || value < 0 || value > MaxMultiplier)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{name} must be between 0 and {MaxMultiplier} (it is {value})."));
            }
        }

        var name = traits.Name ?? string.Empty;
        if (name.Trim().Length == 0)
        {
            errors.Add("Name is missing.");
        }
        else if (name.Length > MaxNameLength || name.Any(char.IsControl))
        {
            errors.Add($"Name must be one line of at most {MaxNameLength} characters.");
        }

        var n = traits.Neediness;
        Range("Neediness.Hunger", n.Hunger);
        Range("Neediness.Happiness", n.Happiness);
        Range("Neediness.Energy", n.Energy);

        var b = traits.Behavior;
        Range("Behavior.Idle", b.Idle);
        Range("Behavior.Walk", b.Walk);
        Range("Behavior.Sit", b.Sit);
        Range("Behavior.Sleep", b.Sleep);
        Range("Behavior.Climb", b.Climb);
        Range("Behavior.Push", b.Push);
        Range("Behavior.Mating", b.Mating);
        if (b.Idle + b.Walk + b.Sit <= 0)
        {
            errors.Add("Behavior.Idle, Behavior.Walk and Behavior.Sit can't all be 0.");
        }

        var c = traits.Chat;
        Range("Chat.Needy", c.Needy);
        Range("Chat.Banter", c.Banter);
        Range("Chat.Jokes", c.Jokes);
        Range("Chat.Notes", c.Notes);
        Range("Chat.Curiosity", c.Curiosity);

        if (traits.Jacket is { } jacket && !JacketColors.TryParse(jacket, out _))
        {
            errors.Add($"Jacket must be a colour like \"#3E455A\" (it is \"{jacket}\").");
        }

        return errors;
    }
}

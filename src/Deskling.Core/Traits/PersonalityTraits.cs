using System.Globalization;
using System.Text.Json;
using Deskling.Core.Abstractions;

namespace Deskling.Core.Traits;

/// <summary>
/// One personality trait a new pet can be given: words for its personality prompt, and how it nudges the pet's
/// numbers. <see cref="Nudge"/> maps a number's name (<c>Hunger</c>, <c>Walk</c>, <c>Mating</c>, <c>Jokes</c> …; see
/// <see cref="PersonalityTraits.Keys"/>) to a factor it's multiplied by.
/// </summary>
/// <param name="Text">Finishes "Mia is a little fire donkey who …", such as "is voracious" or "loves climbing".</param>
/// <param name="Nudge">Factors by number name; numbers it doesn't name are left alone.</param>
public sealed record PersonalityTrait(string Text, IReadOnlyDictionary<string, double> Nudge);

/// <summary>The outcome of reading <c>personality-traits.json</c>: the list, or why it can't be used.</summary>
public sealed record PersonalityTraitsParseResult(IReadOnlyList<PersonalityTrait>? Traits, IReadOnlyList<string> Errors);

/// <summary>
/// The list new pets' personalities are drawn from (<c>personality-traits.json</c> in the data folder, written with
/// <see cref="Defaults"/> when missing). A new pet, added from the menu or hatched from an egg, gets 2 or 3 of them:
/// they make up its personality prompt and nudge its numbers (voracious raises hunger decay, lazy raises the sit and
/// sleep weights, …). Traits that pull the same number opposite ways are never drawn together.
/// </summary>
public static class PersonalityTraits
{
    public const string FileName = "personality-traits.json";

    public const int MaxTextLength = 80;

    /// <summary>The fewest and most traits a new pet gets (fewer if the list is short).</summary>
    public const int MinCount = 2;

    public const int MaxCount = 3;

    public const string Header =
        "// The personality traits new pets are given 2 or 3 of (added from the menu or hatched from an egg).\n" +
        "// Text finishes the sentence \"Mia is a little fire donkey who ...\" and goes into the pet's personality.txt.\n" +
        "// Nudge multiplies the pet's numbers (the Traits in its save.json): Hunger, Happiness, Energy (neediness), Idle, Walk,\n" +
        "// Sit, Sleep, Climb, Push, Mating (behavior), Needy, Banter, Jokes, Notes, Curiosity (chat).\n" +
        "// Traits nudging the same number opposite ways are never given together. Edits apply to the next new pet.\n";

    /// <summary>The numbers a trait can nudge, by name.</summary>
    public static IReadOnlyList<string> Keys { get; } =
        ["Hunger", "Happiness", "Energy", "Idle", "Walk", "Sit", "Sleep", "Climb", "Push", "Mating", "Needy", "Banter", "Jokes", "Notes", "Curiosity"];

    public static IReadOnlyList<PersonalityTrait> Defaults { get; } =
    [
        T("is voracious", ("Hunger", 1.6)),
        T("eats like a bird", ("Hunger", 0.6)),
        T("is easily content", ("Happiness", 0.6), ("Needy", 0.7)),
        T("needs a lot of attention", ("Happiness", 1.6), ("Needy", 1.4)),
        T("is a bit lazy", ("Sit", 1.6), ("Sleep", 1.6), ("Walk", 0.7)),
        T("can't sit still", ("Walk", 1.6), ("Sit", 0.6), ("Idle", 0.7)),
        T("has boundless energy", ("Energy", 0.6), ("Sleep", 0.7)),
        T("tires easily", ("Energy", 1.5), ("Sleep", 1.3)),
        T("loves climbing", ("Climb", 1.8)),
        T("is afraid of heights", ("Climb", 0.4)),
        T("likes shoving windows around", ("Push", 1.8)),
        T("is extremely interested in mating", ("Mating", 2.5)),
        T("isn't interested in romance", ("Mating", 0.4)),
        T("is a chatterbox", ("Banter", 1.6), ("Jokes", 1.2)),
        T("is the quiet type", ("Banter", 0.6), ("Jokes", 0.6)),
        T("loves telling terrible jokes", ("Jokes", 2)),
        T("is nosy about everything on screen", ("Curiosity", 1.8)),
        T("leaves notes everywhere", ("Notes", 1.8)),
        T("is a daydreamer", ("Idle", 1.5), ("Curiosity", 0.7)),
        T("complains a lot", ("Needy", 1.8)),
        T("is very patient", ("Needy", 0.6), ("Idle", 1.3)),
        T("is a bit of a show-off", ("Climb", 1.3), ("Jokes", 1.3)),
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(IReadOnlyList<PersonalityTrait> traits) => Header + JsonSerializer.Serialize(traits, Options) + "\n";

    public static PersonalityTraitsParseResult Parse(string text)
    {
        List<PersonalityTrait?>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<PersonalityTrait?>>(text, Options);
        }
        catch (JsonException e)
        {
            var where = e.LineNumber is { } line ? $" (line {line + 1})" : string.Empty;
            return new PersonalityTraitsParseResult(null, [$"Not a valid list of traits{where}."]);
        }

        var errors = new List<string>();
        var traits = new List<PersonalityTrait>();
        for (var i = 0; i < (raw?.Count ?? 0); i++)
        {
            var where = $"Trait {i + 1}";
            if (raw![i] is not { } trait || string.IsNullOrWhiteSpace(trait.Text))
            {
                errors.Add($"{where} has no Text.");
                continue;
            }

            var words = trait.Text.Trim();
            if (words.Length > MaxTextLength || words.Any(char.IsControl))
            {
                errors.Add($"{where} (\"{words}\"): Text must be one line of at most {MaxTextLength} characters.");
            }

            var nudge = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, factor) in trait.Nudge ?? new Dictionary<string, double>())
            {
                if (Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) is not { } known)
                {
                    errors.Add($"{where} (\"{words}\"): unknown number \"{key}\" (it can be {string.Join(", ", Keys)}).");
                }
                else if (!double.IsFinite(factor) || factor < 0 || factor > PetTraitsFile.MaxMultiplier)
                {
                    errors.Add(string.Create(CultureInfo.InvariantCulture,
                        $"{where} (\"{words}\"): {known} must be between 0 and {PetTraitsFile.MaxMultiplier} (it is {factor})."));
                }
                else
                {
                    nudge[known] = factor;
                }
            }

            traits.Add(new PersonalityTrait(words, nudge));
        }

        return new PersonalityTraitsParseResult(errors.Count == 0 ? traits : null, errors);
    }

    /// <summary>
    /// <see cref="MinCount"/> to <see cref="MaxCount"/> different traits, at random, never two that pull a number
    /// opposite ways (or share their words).
    /// </summary>
    public static IReadOnlyList<PersonalityTrait> Draw(IReadOnlyList<PersonalityTrait> list, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(random);
        var count = MinCount + (int)(random.NextDouble() * (MaxCount - MinCount + 1));
        var pool = list.ToList();
        var drawn = new List<PersonalityTrait>();
        while (drawn.Count < count && pool.Count > 0)
        {
            var pick = pool[System.Math.Min(pool.Count - 1, (int)(random.NextDouble() * pool.Count))];
            pool.Remove(pick);
            if (drawn.All(d => Compatible(d, pick)))
            {
                drawn.Add(pick);
            }
        }

        return drawn;
    }

    /// <summary>Whether two traits can go together: different words, and no number pulled up by one and down by the other.</summary>
    public static bool Compatible(PersonalityTrait a, PersonalityTrait b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var (key, factor) in a.Nudge)
        {
            if (b.Nudge.TryGetValue(key, out var other) && (factor - 1) * (other - 1) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The traits' numbers multiplied by each trait's nudges (kept within 0 to 10, rounded to 2 decimals).</summary>
    public static PetTraits Apply(PetTraits traits, IEnumerable<PersonalityTrait> drawn)
    {
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(drawn);
        var factors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var trait in drawn)
        {
            foreach (var (key, factor) in trait.Nudge)
            {
                factors[key] = factors.GetValueOrDefault(key, 1) * factor;
            }
        }

        double F(string key, double value) =>
            factors.TryGetValue(key, out var f) ? System.Math.Round(System.Math.Clamp(value * f, 0, PetTraitsFile.MaxMultiplier), 2) : value;

        var n = traits.Neediness;
        var b = traits.Behavior;
        var c = traits.Chat;
        return traits with
        {
            Neediness = new Neediness(F("Hunger", n.Hunger), F("Happiness", n.Happiness), F("Energy", n.Energy)),
            Behavior = new BehaviorTraits(
                F("Idle", b.Idle), F("Walk", b.Walk), F("Sit", b.Sit), F("Sleep", b.Sleep), F("Climb", b.Climb), F("Push", b.Push),
                F("Mating", b.Mating)),
            Chat = new ChatTraits(F("Needy", c.Needy), F("Banter", c.Banter), F("Jokes", c.Jokes), F("Notes", c.Notes), F("Curiosity", c.Curiosity)),
        };
    }

    /// <summary>
    /// Finishes a new pet: draws its personality traits, nudges its numbers and writes its personality sentence. With an
    /// empty list it keeps its numbers and gets the sentence from its strongest ones (<see cref="Personality.Describe"/>).
    /// </summary>
    public static (PetTraits Traits, string Personality) Give(PetTraits traits, IReadOnlyList<PersonalityTrait> list, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(traits);
        var drawn = Draw(list, random);
        if (drawn.Count == 0)
        {
            return (traits, Personality.Describe(traits));
        }

        return (Apply(traits, drawn), Sentence(traits.Name, Characters.SpeciesOf(traits.Character), [.. drawn.Select(d => d.Text)]));
    }

    /// <summary>"Mia is a little fire donkey who is voracious, is a bit lazy and loves climbing."</summary>
    /// <param name="name">The pet's name.</param>
    /// <param name="species">What it is (<see cref="CharacterInfo.Species"/>).</param>
    /// <param name="phrases">Its personality traits' words.</param>
    public static string Sentence(string name, string species, IReadOnlyList<string> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        return phrases.Count switch
        {
            0 => $"{name} is a {species} who is pretty easygoing.",
            1 => $"{name} is a {species} who {phrases[0]}.",
            _ => $"{name} is a {species} who {string.Join(", ", phrases.Take(phrases.Count - 1))} and {phrases[^1]}.",
        };
    }

    private static PersonalityTrait T(string text, params (string Key, double Factor)[] nudge) =>
        new(text, nudge.ToDictionary(n => n.Key, n => n.Factor, StringComparer.OrdinalIgnoreCase));
}

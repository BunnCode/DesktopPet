namespace DesktopPet.Core.Traits;

/// <summary>
/// A pet's personality prompt (<c>personality.txt</c>): plain text the user can rewrite, used by chat later. A new pet
/// starts with a sentence that matches its rolled traits.
/// </summary>
public static class Personality
{
    public const string FileName = "personality.txt";

    /// <summary>Longest personality kept, in characters; anything longer is cut.</summary>
    public const int MaxLength = 2_000;

    /// <summary>A multiplier at least this far from 1 (as a factor) counts as a notable trait.</summary>
    private const double Notable = 1.4;

    /// <summary>Up to three of the most pronounced traits, in words.</summary>
    public static string Describe(PetTraits traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        var n = traits.Neediness;
        var b = traits.Behavior;

        // (how pronounced, phrase if high, phrase if low)
        (double Value, string High, string? Low)[] candidates =
        [
            (n.Hunger, "is always hungry", "eats like a bird"),
            (n.Happiness, "needs a lot of attention", "is easily content"),
            (n.Energy, "tires easily", "has boundless energy"),
            (b.Walk, "can't sit still", null),
            (System.Math.Max(b.Sit, b.Sleep), "is a bit lazy", null),
            (b.Climb, "loves climbing", "doesn't like heights"),
            (b.Push, "likes shoving windows around", null),
        ];

        var phrases = candidates
            .Select(c => (Strength: System.Math.Abs(System.Math.Log(System.Math.Max(c.Value, 0.01))), Phrase: c.Value >= 1 ? c.High : c.Low))
            .Where(c => c.Phrase is not null && c.Strength >= System.Math.Log(Notable))
            .OrderByDescending(c => c.Strength)
            .Take(3)
            .Select(c => c.Phrase!)
            .ToList();

        var name = traits.Name;
        return phrases.Count switch
        {
            0 => $"{name} is a little grey rabbit who is pretty easygoing.",
            1 => $"{name} is a little grey rabbit who {phrases[0]}.",
            _ => $"{name} is a little grey rabbit who {string.Join(", ", phrases[..^1])} and {phrases[^1]}.",
        };
    }

    /// <summary>Tidies text read from the file: trimmed and capped at <see cref="MaxLength"/>.</summary>
    public static string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }
}

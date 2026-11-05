using Deskling.Core.Abstractions;

namespace Deskling.Core.Traits;

/// <summary>A built-in character: a sprite sheet embedded in the app.</summary>
/// <param name="Id">What <see cref="PetTraits.Character"/> holds, and the sheet's file name without ".png".</param>
/// <param name="Name">Its name for people.</param>
/// <param name="Species">What it is, finishing "Mia is a …" and "You are Mia, a …".</param>
public sealed record CharacterInfo(string Id, string Name, string Species);

/// <summary>
/// The built-in characters. A pet whose <see cref="PetTraits.Character"/> is unset wears one of the
/// <see cref="Defaults"/> picked by its number; new pets get one of them (the least worn); a hatched pet takes after
/// the parent it got its art from. The rabbit is still built in, but only for pets set to it by hand.
/// </summary>
public static class Characters
{
    public const string Rabbit = "rabbit";

    /// <summary>The species for a pet whose character isn't known.</summary>
    public const string UnknownSpecies = "little critter";

    /// <summary>The characters pets get unless told otherwise (the sheets in SpriteSheet/Fakemon).</summary>
    public static IReadOnlyList<CharacterInfo> Defaults { get; } =
    [
        new("botrosa", "Botrosa", "little mechanical creature with a floating ring"),
        new("carycajolt", "Carycajolt", "little electric caracal-like cat"),
        new("courceel", "Courceel", "little tan foal"),
        new("crimejr", "Crime Jr.", "little bear-cub thief in a hood"),
        new("emberdonkey", "Emberdonkey", "little fire donkey"),
        new("redmuzzle", "Redmuzzle", "stocky little dark bear with a big red muzzle"),
    ];

    /// <summary>Every built-in character: the <see cref="Defaults"/>, then the rabbit.</summary>
    public static IReadOnlyList<CharacterInfo> All { get; } =
        [.. Defaults, new(Rabbit, "Rabbit", "little grey rabbit")];

    /// <summary>The character with this id (any case), or null.</summary>
    public static CharacterInfo? Find(string? id)
    {
        var key = id?.Trim();
        return string.IsNullOrEmpty(key) ? null : All.FirstOrDefault(c => string.Equals(c.Id, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The character pet <paramref name="petId"/> wears: its own if it has a known one, else its number's default.</summary>
    public static CharacterInfo For(int petId, string? character) =>
        Find(character) ?? Defaults[(((petId - 1) % Defaults.Count) + Defaults.Count) % Defaults.Count];

    /// <summary>What a pet with this character is (<see cref="UnknownSpecies"/> if it isn't known).</summary>
    public static string SpeciesOf(string? character) => Find(character)?.Species ?? UnknownSpecies;

    /// <summary>A default character for a new pet: one the fewest existing pets wear, at random among those.</summary>
    /// <param name="random">Randomness.</param>
    /// <param name="worn">The characters existing pets wear (ids).</param>
    public static string Pick(IRandom random, IEnumerable<string> worn)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(worn);
        var counts = Defaults.ToDictionary(c => c.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var id in worn)
        {
            if (id is not null && counts.TryGetValue(id, out var n))
            {
                counts[id] = n + 1;
            }
        }

        var fewest = counts.Values.Min();
        var choices = Defaults.Where(c => counts[c.Id] == fewest).ToList();
        return choices[System.Math.Min((int)(random.NextDouble() * choices.Count), choices.Count - 1)].Id;
    }
}

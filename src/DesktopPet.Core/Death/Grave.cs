using System.Globalization;
using System.Text.Json.Serialization;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Persistence;

namespace DesktopPet.Core.Death;

/// <summary>
/// Whose gravestone it is. Saved with the gravestone in <c>room.json</c>, but the pet's folder (its save, marked dead)
/// is what counts: at startup every dead pet gets a gravestone, and these details are refreshed from its folder.
/// </summary>
public sealed record GraveInfo
{
    /// <summary>The dead pet's id (its folder name).</summary>
    public int PetId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>When it was born or added; null if unknown.</summary>
    public DateTimeOffset? BornUtc { get; init; }

    public DateTimeOffset DiedUtc { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<DeathCause>))]
    public DeathCause Cause { get; init; }

    /// <summary>The details for a dead pet from its save; null if the save isn't marked dead.</summary>
    public static GraveInfo? From(int petId, string name, SaveData save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save.DiedUtc is { } died
            ? new GraveInfo { PetId = petId, Name = name, BornUtc = save.BornUtc, DiedUtc = died, Cause = save.DiedOf ?? DeathCause.Neglect }
            : null;
    }

    /// <summary>"Mia went too long without food."</summary>
    [JsonIgnore]
    public string Story => Cause switch
    {
        DeathCause.Hunger => $"{Name} went too long without food.",
        DeathCause.Sadness => $"{Name} was unhappy for too long.",
        DeathCause.Exhaustion => $"{Name} went too long without sleep.",
        _ => $"{Name} was neglected for too long.",
    };

    /// <summary>"3 Nov 2026" in the given culture's month names.</summary>
    public static string Date(DateTimeOffset utc, CultureInfo culture) =>
        utc.ToLocalTime().ToString("d MMM yyyy", culture);
}

/// <summary>The gravestone as a world object: a piece of built-in furniture that pets don't use. It can be dragged.</summary>
public static class Graves
{
    /// <summary>The gravestone's furniture kind; a furniture file of this name is ignored.</summary>
    public const string Kind = "grave";

    /// <summary>64×60 art pixels (1 DIP each), drawn by the sprite generator; the app writes the name and dates on it.</summary>
    public static FurnitureSpec Spec { get; } = new()
    {
        Kind = Kind, Name = "Gravestone", WidthDip = 64, HeightDip = 60, Art = "grave.png", Frames = 1, Slots = 0,
        Friction = 2,
    };
}

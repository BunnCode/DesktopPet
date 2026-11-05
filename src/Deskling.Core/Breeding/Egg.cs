using System.Text.Json.Serialization;
using Deskling.Core.Chat;
using Deskling.Core.Furniture;
using Deskling.Core.Traits;

namespace Deskling.Core.Breeding;

/// <summary>
/// What an egg holds: when it was laid and hatches, who laid it, and the child it hatches into. Saved with the egg in
/// <c>room.json</c>, so it survives restarts and hatches on time even if the app was closed.
/// </summary>
public sealed record EggInfo
{
    public DateTimeOffset LaidAtUtc { get; init; }

    public DateTimeOffset HatchAtUtc { get; init; }

    /// <summary>The parents' names.</summary>
    public TextList Parents { get; init; } = [];

    /// <summary>
    /// The child's inherited numbers, jacket colour and parents. Its name and personality traits are given when it
    /// hatches, so the name avoids every pet there is by then.
    /// </summary>
    public PetTraits Child { get; init; } = new();

    /// <summary>The parent (pet id) whose own sprite sheet the child copies; null for the shared or built-in art.</summary>
    public int? SpritesFrom { get; init; }

    /// <summary>"Mia and Leo".</summary>
    [JsonIgnore]
    public string ParentNames => Parents.Count switch
    {
        0 => "two pets",
        1 => Parents[0],
        _ => $"{string.Join(", ", Parents.Take(Parents.Count - 1))} and {Parents[^1]}",
    };
}

/// <summary>The egg as a world object: a small piece of built-in furniture that pets don't use. It can be dragged.</summary>
public static class Eggs
{
    /// <summary>The egg's furniture kind; a furniture file of this name is ignored.</summary>
    public const string Kind = "egg";

    /// <summary>24×26 art pixels (1 DIP each), drawn by the sprite generator: two still frames, then a wobble.</summary>
    public static FurnitureSpec Spec { get; } = new()
    {
        Kind = Kind, Name = "Egg", WidthDip = 24, HeightDip = 26, Art = "egg.png", Frames = 4, Fps = 1.5,
        Animate = AnimateWhen.Always, Slots = 0,
    };
}

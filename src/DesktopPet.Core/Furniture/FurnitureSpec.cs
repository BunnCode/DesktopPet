using System.Text.Json.Serialization;
using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Furniture;

/// <summary>A pet need that furniture can meet.</summary>
public enum Need
{
    /// <summary>Meets no need (just fun, or the play pen).</summary>
    None,
    Hunger,
    Happiness,
    Energy,
}

/// <summary>What using a piece of furniture does besides playing an animation.</summary>
public enum UseEffect
{
    /// <summary>The pet stays at its slot.</summary>
    None,

    /// <summary>Little hops on the spot for the whole use (jumping on the couch).</summary>
    Hop,

    /// <summary>A few hops, then a big launch straight up (trampoline): a quick way onto tall window tops.</summary>
    Bounce,

    /// <summary>Chases the item and kicks it around (a ball). The item must be free to move.</summary>
    Kick,

    /// <summary>
    /// A pen: pets never choose it. A pet the user drops inside can't leave until the user takes it out, and its needs
    /// don't drop while it's in there.
    /// </summary>
    Confine,
}

/// <summary>When the furniture's own frames play.</summary>
public enum AnimateWhen
{
    Always,

    /// <summary>Only while it moves (a ball rolling); otherwise frame 0.</summary>
    Moving,

    /// <summary>Only while a pet is using it (a TV that's on); otherwise frame 0.</summary>
    InUse,
}

/// <summary>One way a pet can use a piece of furniture.</summary>
public sealed record FurnitureUse
{
    /// <summary>The need it meets; pets pick it when that need is low.</summary>
    public Need Need { get; init; }

    /// <summary>
    /// Points per hour of use added to <see cref="Need"/>, on top of what the pet's activity does anyway (sleeping
    /// still restores energy at the normal rate). Kept below the care actions so the user's care stays the best option.
    /// </summary>
    public double RatePerHour { get; init; }

    /// <summary>The pet's animation while using it (an id from <c>Animations.All</c>, such as <c>eat</c> or <c>sleep</c>).</summary>
    public string Animation { get; init; } = "idle";

    /// <summary>How long one use lasts, seconds. It ends early once the need is full.</summary>
    public DurationRange Seconds { get; init; } = new(20, 40);

    public UseEffect Effect { get; init; }

    /// <summary>How far above the floor the pet sits while using it (on the bed or couch), DIP. Drawing only.</summary>
    public double SeatDip { get; init; }
}

/// <summary>
/// A kind of furniture, from one JSON file in the <c>furniture</c> folder (the file name is its <see cref="Kind"/>).
/// Sizes are DIPs, like the pet's.
/// </summary>
public sealed record FurnitureSpec
{
    /// <summary>Identity: the definition's file name without <c>.json</c>, lower case. Saved positions refer to it.</summary>
    [JsonIgnore]
    public string Kind { get; init; } = string.Empty;

    /// <summary>Shown in menus.</summary>
    public string Name { get; init; } = string.Empty;

    public double WidthDip { get; init; } = 32;

    public double HeightDip { get; init; } = 32;

    /// <summary>The image, next to the definition. Frames side by side, left to right, all the same width.</summary>
    public string Art { get; init; } = string.Empty;

    public int Frames { get; init; } = 1;

    public double Fps { get; init; } = 4;

    public AnimateWhen Animate { get; init; }

    /// <summary>Scale with smoothing (painted art) rather than nearest-neighbour (pixel art).</summary>
    public bool Smooth { get; init; }

    /// <summary>How many pets can use it at once.</summary>
    public int Slots { get; init; } = 1;

    /// <summary>
    /// Where each slot's pet stands, DIP from the furniture's centre (negative = left). Pets beside the furniture
    /// face it. Without this the slots are spread evenly across its width.
    /// </summary>
    public IReadOnlyList<double>? SlotOffsetsDip { get; init; }

    /// <summary>Fraction of its fall speed kept when it lands (a ball bounces; 0 for most furniture).</summary>
    public double Bounciness { get; init; }

    /// <summary>Multiplies the ground friction when it slides (a ball rolls far; 1 for most furniture).</summary>
    public double Friction { get; init; } = 1;

    public IReadOnlyList<FurnitureUse> Uses { get; init; } = [];

    /// <summary>Whether this is a pen (pets dropped inside stay inside).</summary>
    [JsonIgnore]
    public bool IsPen => Uses.Any(u => u.Effect == UseEffect.Confine);

    /// <summary>Slot <paramref name="slot"/>'s offset from the centre, DIP.</summary>
    public double SlotOffsetDip(int slot)
    {
        if (SlotOffsetsDip is { } offsets && slot < offsets.Count)
        {
            return offsets[slot];
        }

        return Slots <= 1 ? 0 : -WidthDip / 2 + (WidthDip * (slot + 0.5) / Slots);
    }
}

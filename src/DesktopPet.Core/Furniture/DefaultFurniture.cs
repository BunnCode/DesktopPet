namespace DesktopPet.Core.Furniture;

/// <summary>
/// The furniture written to the <c>furniture</c> folder on first run. Each art file is drawn by the sprite generator
/// (<c>tools/sprites/make_rabbit.py</c>) at 1 art pixel = 2 DIP, like the rabbit.
/// Rates stay below the care actions (Feed is +35 at once, Play +22), so caring for a pet stays the best option.
/// </summary>
public static class DefaultFurniture
{
    public static FurnitureSpec Ball { get; } = new()
    {
        Kind = "ball", Name = "Ball", WidthDip = 16, HeightDip = 16, Art = "ball.png", Frames = 4, Fps = 12,
        Animate = AnimateWhen.Moving, Slots = 2, Bounciness = 0.5, Friction = 0.1,
        Uses = [new() { Need = Need.Happiness, RatePerHour = 1500, Animation = "kick", Seconds = new(15, 30), Effect = UseEffect.Kick }],
    };

    public static FurnitureSpec Tv { get; } = new()
    {
        Kind = "tv", Name = "TV", WidthDip = 48, HeightDip = 40, Art = "tv.png", Frames = 3, Fps = 4,
        Animate = AnimateWhen.InUse, Slots = 4, SlotOffsetsDip = [-90, -58, 58, 90],
        Uses = [new() { Need = Need.Happiness, RatePerHour = 900, Animation = "game", Seconds = new(30, 90) }],
    };

    public static FurnitureSpec Couch { get; } = new()
    {
        Kind = "couch", Name = "Couch", WidthDip = 80, HeightDip = 36, Art = "couch.png", Slots = 2, SlotOffsetsDip = [-18, 18],
        Uses =
        [
            new() { Need = Need.Energy, RatePerHour = 5, Animation = "sleep", Seconds = new(120, 300), SeatDip = 16 },
            new() { Need = Need.Happiness, RatePerHour = 1200, Animation = "bounce", Seconds = new(6, 12), Effect = UseEffect.Hop, SeatDip = 16 },
        ],
    };

    public static FurnitureSpec Trampoline { get; } = new()
    {
        Kind = "trampoline", Name = "Trampoline", WidthDip = 64, HeightDip = 20, Art = "trampoline.png",
        Uses = [new() { Need = Need.Happiness, RatePerHour = 600, Animation = "bounce", Seconds = new(10, 10), Effect = UseEffect.Bounce, SeatDip = 16 }],
    };

    public static FurnitureSpec Bed { get; } = new()
    {
        Kind = "bed", Name = "Bed", WidthDip = 80, HeightDip = 32, Art = "bed.png",
        Uses = [new() { Need = Need.Energy, RatePerHour = 12, Animation = "sleep", Seconds = new(300, 900), SeatDip = 16 }],
    };

    public static FurnitureSpec PlayPen { get; } = new()
    {
        Kind = "playpen", Name = "Play pen", WidthDip = 112, HeightDip = 40, Art = "playpen.png", Slots = 0,
        Uses = [new() { Need = Need.None, Effect = UseEffect.Confine, Seconds = new(0, 0) }],
    };

    public static FurnitureSpec Trough { get; } = new()
    {
        Kind = "trough", Name = "Trough", WidthDip = 48, HeightDip = 20, Art = "trough.png", Slots = 2, SlotOffsetsDip = [-40, 40],
        Uses = [new() { Need = Need.Hunger, RatePerHour = 2400, Animation = "eat", Seconds = new(15, 25) }],
    };

    public static IReadOnlyList<FurnitureSpec> All { get; } = [Ball, Tv, Couch, Trampoline, Bed, PlayPen, Trough];
}

namespace Deskling.Core.Furniture;

/// <summary>
/// The furniture written to the <c>furniture</c> folder on first run. Each art file is drawn by the sprite generator
/// (<c>tools/sprites/make_rabbit.py</c>) at 1 art pixel = 1 DIP, like the rabbit.
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

    /// <summary>A blue pill, used up once swallowed: the pet then asks every other pet to mate (M15).</summary>
    public static FurnitureSpec Pill { get; } = new()
    {
        Kind = "pill", Name = "Breeding pill", WidthDip = 20, HeightDip = 12, Art = "pill.png", Consumable = true,
        Uses = [new() { Need = Need.None, Animation = "eat", Seconds = new(2, 3), Effect = UseEffect.Breed }],
    };

    /// <summary>
    /// Verner Panton's Living Tower (1969): two upholstered blocks with one flowing opening through them, sat in at four
    /// levels, upright or reclined. Pets hang out together there (sitting on a seat's edge, legs dangling) or nap. Each
    /// slot has its own seat, measured from the art: the left shelf, the floor, the floor's rising right end and the
    /// right shelf, each pet turned towards the middle.
    /// </summary>
    public static FurnitureSpec LivingTower { get; } = new()
    {
        Kind = "livingtower", Name = "Living Tower", WidthDip = 128, HeightDip = 128, Art = "livingtower.png", Slots = 4,
        SlotOffsetsDip = [-30, -4, 32, 10], SlotSeatsDip = [50, 24, 32, 77],
        SlotFacing = [SlotFacing.Right, SlotFacing.Right, SlotFacing.Left, SlotFacing.Left],
        Uses =
        [
            new() { Need = Need.Happiness, RatePerHour = 700, Animation = "sit_ledge", Seconds = new(30, 90), MinSeconds = 30 },
            new() { Need = Need.Energy, RatePerHour = 5, Animation = "sleep", Seconds = new(120, 300), MinSeconds = 30 },
        ],
    };

    public static IReadOnlyList<FurnitureSpec> All { get; } = [Ball, Tv, Couch, Trampoline, Bed, PlayPen, Trough, Pill, LivingTower];

    /// <summary>The kinds written by the first version with furniture (M11), before the list of written defaults was kept.</summary>
    public static IReadOnlyList<string> FirstKinds { get; } = ["ball", "tv", "couch", "trampoline", "bed", "playpen", "trough"];

    /// <summary>
    /// The defaults to write into an existing furniture folder: those added since it was last written to. A default
    /// the user deleted stays deleted. <paramref name="written"/> is the list kept in the folder, or null if there's
    /// none (a folder from before the list was kept, which has the <see cref="FirstKinds"/>).
    /// </summary>
    public static IReadOnlyList<FurnitureSpec> ToWrite(IReadOnlyCollection<string>? written)
    {
        var done = new HashSet<string>(written ?? FirstKinds, StringComparer.OrdinalIgnoreCase);
        return [.. All.Where(s => !done.Contains(s.Kind))];
    }
}

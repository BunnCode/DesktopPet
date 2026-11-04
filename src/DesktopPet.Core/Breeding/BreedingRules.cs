namespace DesktopPet.Core.Breeding;

/// <summary>
/// Tuning for mating and eggs (<c>settings.json</c> under <c>Breeding</c>). Mating is rare and automatic: two pets
/// standing close together on the same surface, both doing fine, both past their cooldown, have a small chance each
/// hour. A short courtship follows, and an egg appears between them.
/// </summary>
public sealed record BreedingRules
{
    /// <summary>Whether pets mate at all (eggs already laid still hatch).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The chance of mating, per hour that a pair spends close together with good stats, for two average pets
    /// (scaled by their Mating traits). 0.5 means about once every two hours of that.
    /// </summary>
    public double MatesPerHour { get; init; } = 0.5;

    /// <summary>A pet that mated (or hatched) doesn't mate again for this many hours.</summary>
    public double CooldownHours { get; init; } = 24;

    /// <summary>Every stat of both pets must be at least this.</summary>
    public double MinStat { get; init; } = 60;

    /// <summary>How close their feet must be, DIP, on the same surface.</summary>
    public double ReachDip { get; init; } = 160;

    /// <summary>How long they gaze at each other with hearts once they've met, seconds.</summary>
    public double CourtshipSeconds { get; init; } = 6;

    /// <summary>Walking up to each other takes longer than this, seconds: the courtship is off.</summary>
    public double ApproachSeconds { get; init; } = 15;

    /// <summary>How long an egg takes to hatch, minutes (also counts while the app is closed).</summary>
    public double HatchMinutes { get; init; } = 60;

    /// <summary>
    /// How far a child's numbers may stray from what it inherits, as a factor either way: 0.15 means anywhere from
    /// ÷1.15 to ×1.15.
    /// </summary>
    public double Variation { get; init; } = 0.15;

    /// <summary>
    /// A pet that swallowed a breeding pill goes round asking the others for this long at most, minutes. The pill
    /// overrides its own cooldown and stats, but not being fixed.
    /// </summary>
    public double PillMinutes { get; init; } = 10;

    /// <summary>How long it tries to reach and ask one pet before moving on to the next, seconds.</summary>
    public double PillVisitSeconds { get; init; } = 45;

    /// <summary>How long it asks (gazing with hearts) before the other pet answers, seconds.</summary>
    public double PillAskSeconds { get; init; } = 3;

    /// <summary>
    /// The chance an asked pet says yes, for an average pet (scaled by its Mating trait, at most 1). It can only say
    /// yes if it could mate anyway: not fixed, past its cooldown, its stats at least <see cref="MinStat"/>.
    /// </summary>
    public double PillAgreeChance { get; init; } = 0.75;
}

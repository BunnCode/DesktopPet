namespace Deskling.Core.Death;

/// <summary>
/// Tuning for death (<c>settings.json</c> under <c>Death</c>). A pet dies only of neglect while the user is at the PC:
/// time away (locked screen, PC asleep, app closed) and time shut in a pen never count.
/// </summary>
public sealed record DeathRules
{
    /// <summary>Whether pets can die at all. Off: no countdown and no warnings about it.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>A pet dies when one of its needs stays at 0 for this many hours (of the user being there).</summary>
    public double ZeroHours { get; init; } = 2;

    /// <summary>...or when all three needs stay critical together for this many hours.</summary>
    public double AllCriticalHours { get; init; } = 4;

    /// <summary>A revived pet comes back with every need at this.</summary>
    public double ReviveStat { get; init; } = 60;
}

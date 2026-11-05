using System.Text.Json.Serialization;
using Deskling.Core.Furniture;
using Deskling.Core.Stats;

namespace Deskling.Core.Death;

/// <summary>What a pet died of.</summary>
public enum DeathCause
{
    /// <summary>Hunger stayed at 0.</summary>
    Hunger,

    /// <summary>Happiness stayed at 0.</summary>
    Sadness,

    /// <summary>Energy stayed at 0.</summary>
    Exhaustion,

    /// <summary>All three needs stayed critical.</summary>
    Neglect,
}

/// <summary>A pet died: of what and when.</summary>
public sealed record PetDeath(DeathCause Cause, DateTimeOffset AtUtc);

/// <summary>What a warning is about.</summary>
public enum WarningKind
{
    /// <summary>A need dropped below critical.</summary>
    Critical,

    /// <summary>A need reached 0: the countdown to death has started.</summary>
    AtZero,

    /// <summary>All three needs are critical: a slower countdown has started.</summary>
    AllCritical,
}

/// <summary>A warning for the user (a tray notification).</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Need">The need it's about; <see cref="Need.None"/> for <see cref="WarningKind.AllCritical"/>.</param>
/// <param name="HoursLeft">For the countdowns, how long until the pet dies if nothing changes.</param>
public sealed record PetWarning(WarningKind Kind, Need Need, double HoursLeft = 0);

/// <summary>
/// How long, in seconds of the user being there, each need has been at 0 and all three have been critical together.
/// Saved with the pet, so restarting the app doesn't reset a countdown.
/// </summary>
public sealed record NeglectTimers
{
    public double HungerAtZero { get; init; }

    public double HappinessAtZero { get; init; }

    public double EnergyAtZero { get; init; }

    public double AllCritical { get; init; }

    [JsonIgnore]
    public bool IsZero => HungerAtZero == 0 && HappinessAtZero == 0 && EnergyAtZero == 0 && AllCritical == 0;
}

/// <summary>
/// Counts a pet's neglect and decides when it dies (M14), and when to warn the user first. A need at 0 counts toward
/// <see cref="DeathRules.ZeroHours"/>; all three critical together count toward <see cref="DeathRules.AllCriticalHours"/>.
/// A need rising off 0 (or out of critical) resets its timer. Time that doesn't count (the user away, the pet in a
/// pen) pauses the timers. Each warning is given once until the need recovers.
/// </summary>
public sealed class Mortality(NeglectTimers? timers = null)
{
    /// <summary>A need must climb this far back above critical before it can warn about critical again.</summary>
    internal const double Hysteresis = 5;

    private static readonly Need[] Needs = [Need.Hunger, Need.Happiness, Need.Energy];

    private readonly bool[] _criticalWarned = new bool[3];
    private readonly bool[] _zeroWarned = new bool[3];
    private bool _allWarned;

    public NeglectTimers Timers { get; private set; } = timers ?? new NeglectTimers();

    /// <summary>
    /// Advances the timers by <paramref name="elapsed"/> with the pet's needs at <paramref name="stats"/> (after that
    /// time). Returns what it died of, or null if it lives on.
    /// </summary>
    /// <param name="stats">The pet's needs now.</param>
    /// <param name="elapsed">Real time since the last update.</param>
    /// <param name="counting">The time counts: the user is there and the pet isn't shut in a pen.</param>
    /// <param name="rules">Death tuning.</param>
    /// <param name="critical">Below this a need is critical (<see cref="StatRules.Critical"/>).</param>
    /// <param name="warnings">New warnings are added here (only while counting).</param>
    public DeathCause? Update(PetStats stats, TimeSpan elapsed, bool counting, DeathRules rules, double critical, ICollection<PetWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(warnings);
        if (!rules.Enabled)
        {
            Timers = new NeglectTimers();
            Array.Clear(_criticalWarned);
            Array.Clear(_zeroWarned);
            _allWarned = false;
            return null;
        }

        var seconds = counting ? System.Math.Max(0, elapsed.TotalSeconds) : 0;
        double[] values = [stats.Hunger, stats.Happiness, stats.Energy];
        double[] zero = [Timers.HungerAtZero, Timers.HappinessAtZero, Timers.EnergyAtZero];
        var zeroLimit = rules.ZeroHours * 3600;
        for (var i = 0; i < 3; i++)
        {
            var value = values[i];
            zero[i] = value <= 0 ? zero[i] + seconds : 0;

            if (value >= critical)
            {
                _zeroWarned[i] = false;
            }

            if (value >= critical + Hysteresis)
            {
                _criticalWarned[i] = false;
            }

            if (!counting)
            {
                continue;
            }

            if (value <= 0 && !_zeroWarned[i])
            {
                _zeroWarned[i] = _criticalWarned[i] = true; // one warning, not "critical" and then "at zero"
                warnings.Add(new PetWarning(WarningKind.AtZero, Needs[i], Hours(zeroLimit - zero[i])));
            }
            else if (value < critical && !_criticalWarned[i])
            {
                _criticalWarned[i] = true;
                warnings.Add(new PetWarning(WarningKind.Critical, Needs[i]));
            }
        }

        var allCritical = values.All(v => v < critical);
        var all = allCritical ? Timers.AllCritical + seconds : 0;
        var allLimit = rules.AllCriticalHours * 3600;
        if (!allCritical)
        {
            _allWarned = false;
        }
        else if (counting && !_allWarned)
        {
            _allWarned = true;
            warnings.Add(new PetWarning(WarningKind.AllCritical, Need.None, Hours(allLimit - all)));
        }

        Timers = new NeglectTimers { HungerAtZero = zero[0], HappinessAtZero = zero[1], EnergyAtZero = zero[2], AllCritical = all };

        for (var i = 0; i < 3; i++)
        {
            if (values[i] <= 0 && zero[i] >= zeroLimit)
            {
                return (DeathCause)i; // Hunger, Sadness, Exhaustion in the needs' order
            }
        }

        return allCritical && all >= allLimit ? DeathCause.Neglect : null;
    }

    private static double Hours(double seconds) => System.Math.Max(0, seconds) / 3600;
}

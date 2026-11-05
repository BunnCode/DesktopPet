namespace Deskling.Core.Stats;

/// <summary>Pure stat arithmetic: decay over time, the away floor, and action effects.</summary>
public static class StatsSystem
{
    /// <summary>Advances stats by <paramref name="elapsed"/> of real time.</summary>
    /// <param name="stats">Current stats.</param>
    /// <param name="elapsed">Real time that passed.</param>
    /// <param name="activity">What the pet was doing.</param>
    /// <param name="away">
    /// The user was away (locked, asleep, off, app closed). Decay still happens but never pushes a stat below
    /// <see cref="StatRules.AwayFloor"/>; a stat already below it stays where it is.
    /// </param>
    /// <param name="rules">Rates and thresholds.</param>
    public static PetStats Advance(PetStats stats, TimeSpan elapsed, StatActivity activity, bool away, StatRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (elapsed <= TimeSpan.Zero)
        {
            return stats;
        }

        var hours = elapsed.TotalHours;
        var decay = activity == StatActivity.Sleeping ? rules.SleepDecayFactor : 1.0;

        var energyRate = activity switch
        {
            StatActivity.Sleeping => rules.SleepEnergyPerHour,
            StatActivity.Active => rules.EnergyPerHour + rules.ActiveEnergyPerHour,
            _ => rules.EnergyPerHour,
        };

        var next = new PetStats(
            stats.Hunger + (rules.HungerPerHour * decay * hours),
            stats.Happiness + (rules.HappinessPerHour * decay * hours),
            stats.Energy + (energyRate * hours)).Clamped();

        return away ? ApplyFloor(stats, next, rules.AwayFloor) : next;
    }

    /// <summary>
    /// Offline catch-up after the app was closed: counts as away and as sleeping (the pet naps while you're gone).
    /// </summary>
    public static PetStats CatchUp(PetStats saved, TimeSpan offline, StatRules rules) =>
        Advance(saved, offline, StatActivity.Sleeping, away: true, rules);

    public static PetStats Apply(PetStats stats, PetAction action, StatRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return action switch
        {
            PetAction.Feed => (stats with
            {
                Hunger = stats.Hunger + rules.FeedHunger,
                Happiness = stats.Happiness + rules.FeedHappiness,
            }).Clamped(),
            PetAction.Play => (stats with
            {
                Happiness = stats.Happiness + rules.PlayHappiness,
                Energy = stats.Energy + rules.PlayEnergy,
                Hunger = stats.Hunger + rules.PlayHunger,
            }).Clamped(),
            _ => stats,
        };
    }

    public static PetStats Petted(PetStats stats, StatRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return (stats with { Happiness = stats.Happiness + rules.PetHappiness }).Clamped();
    }

    /// <summary>Per stat: if it fell, it may not fall below the floor (or below where it already was, if lower).</summary>
    private static PetStats ApplyFloor(PetStats before, PetStats after, double floor)
    {
        static double Floored(double was, double now, double floor) =>
            now >= was ? now : System.Math.Max(now, System.Math.Min(was, floor));

        return new PetStats(
            Floored(before.Hunger, after.Hunger, floor),
            Floored(before.Happiness, after.Happiness, floor),
            Floored(before.Energy, after.Energy, floor));
    }
}

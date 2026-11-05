using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Furniture;

namespace DesktopPet.Core.Behavior;

/// <summary>
/// Pets joining each other's games: a pet idling, walking or sitting near another pet playing a group game (a fun use
/// of furniture with more than one slot, one still free, such as the TV) may come over and play along for a little
/// while (<see cref="Simulation.FurnitureRules.JoinSeconds"/>).
/// </summary>
internal static class GroupPlay
{
    /// <summary>
    /// Rolls whether the pet joins the nearest group game in reach this tick and, if so, sets off for it
    /// (<see cref="PetContext.Errand"/>). Call only while it's idling, walking or sitting. Nothing is rolled when there's
    /// no game nearby.
    /// </summary>
    public static bool TryJoin(PetContext context, double dt)
    {
        var rules = context.Config.Furniture;
        if (rules.JoinChancePerSecond <= 0 || context.Time < context.NoJoinUntil || context.Courtship is not null
            || !FurnitureChooser.CanChoose(context) || AnyCritical(context))
        {
            return false;
        }

        if (Nearest(context) is not var (item, use))
        {
            return false;
        }

        var chance = 1 - System.Math.Pow(1 - System.Math.Min(rules.JoinChancePerSecond, 1), dt);
        if (context.Random.NextDouble() >= chance)
        {
            return false;
        }

        var seconds = context.Random.Range(rules.JoinSeconds.Min, rules.JoinSeconds.Max);
        if (!FurnitureChooser.TryGo(context, item, use, seconds))
        {
            return false;
        }

        context.NoJoinUntil = context.Time + rules.JoinCooldownSeconds;
        return true;
    }

    /// <summary>The nearest group game in reach that it could join, and the use being played there.</summary>
    private static (FurnitureItem Item, FurnitureUse Use)? Nearest(PetContext context)
    {
        var range = context.Config.Furniture.JoinDistanceDip * context.Scale;
        (FurnitureItem, FurnitureUse)? best = null;
        var bestDistance = double.MaxValue;
        foreach (var item in context.Furniture.Items)
        {
            if (item.Spec.Slots < 2 || item.IsHeld || item.GroundKey is null || !item.HasFreeSlot(context.Owner)
                || (context.Unreachable.TryGetValue(item.Id, out var retryAt) && retryAt > context.Time))
            {
                continue;
            }

            var distance = (item.Feet - context.Body.Feet).Length;
            if (distance > range || distance >= bestDistance)
            {
                continue;
            }

            foreach (var (user, use) in item.Users)
            {
                if (!ReferenceEquals(user, context.Owner) && FurnitureChooser.IsFun(use)
                    && (use.Need == Need.None || FurnitureChooser.Value(use.Need, context.Stats) < FurnitureChooser.Full))
                {
                    best = (item, use);
                    bestDistance = distance;
                    break;
                }
            }
        }

        return best;
    }

    /// <summary>A need is critical: it has better things to do than play.</summary>
    private static bool AnyCritical(PetContext context)
    {
        var stats = context.Stats;
        var critical = context.Config.Stats.Critical;
        return stats.Hunger < critical || stats.Happiness < critical || stats.Energy < critical;
    }
}

using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Breeding;

/// <summary>A pet as breeding sees it: its id (folder number) and its simulation.</summary>
public sealed record BreedingPet(int Id, PetSimulation Sim);

/// <summary>Two pets mated: lay an egg at <paramref name="EggFeet"/> (physical pixels; it drops onto the surface below).</summary>
public sealed record Mating(int ParentA, int ParentB, Vec2 EggFeet);

/// <summary>
/// Decides when pets mate (M13). Each tick, every pair standing close together on the same surface, both free, both
/// doing fine, neither fixed and both past their cooldown, gets a small chance (<see cref="BreedingRules.MatesPerHour"/>,
/// scaled by their Mating traits). One courtship runs at a time: the two walk up to each other, gaze with hearts for
/// <see cref="BreedingRules.CourtshipSeconds"/>, and then the host lays an egg between them (<see cref="TakeMatings"/>).
/// If either is picked up, falls or otherwise stops, or they can't meet in time, it's off and nothing happens.
/// </summary>
public sealed class BreedingDirector(IRandom random, IClock clock)
{
    private readonly IRandom _random = random ?? throw new ArgumentNullException(nameof(random));
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly List<Mating> _matings = [];
    private Courting? _courting;

    /// <summary>The pair courting right now, if any.</summary>
    public (int A, int B)? Courtship => _courting is { } c ? (c.A, c.B) : null;

    /// <param name="dt">Seconds since the last tick.</param>
    /// <param name="pets">Every pet.</param>
    /// <param name="rules">Current tuning.</param>
    /// <param name="roomForEgg">Whether another egg fits under the pet cap (eggs count toward it).</param>
    public void Tick(double dt, IReadOnlyList<BreedingPet> pets, BreedingRules rules, bool roomForEgg)
    {
        ArgumentNullException.ThrowIfNull(pets);
        ArgumentNullException.ThrowIfNull(rules);
        if (_courting is { } courting)
        {
            Continue(courting, dt, pets, rules);
            return;
        }

        if (!rules.Enabled || !roomForEgg || rules.MatesPerHour <= 0 || dt <= 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        var ready = pets.Where(p => Ready(p.Sim, rules, now)).ToList();
        for (var i = 0; i < ready.Count; i++)
        {
            for (var j = i + 1; j < ready.Count; j++)
            {
                var (a, b) = (ready[i], ready[j]);
                if (!Close(a.Sim, b.Sim, rules))
                {
                    continue;
                }

                var rate = rules.MatesPerHour * System.Math.Sqrt(a.Sim.Traits.Behavior.Mating * b.Sim.Traits.Behavior.Mating);
                var chance = 1 - System.Math.Exp(-rate * dt / 3600);
                if (_random.NextDouble() < chance)
                {
                    Start(a, b, rules);
                    return;
                }
            }
        }
    }

    /// <summary>The matings since the last call; the host lays an egg for each.</summary>
    public IReadOnlyList<Mating> TakeMatings()
    {
        if (_matings.Count == 0)
        {
            return [];
        }

        var taken = _matings.ToArray();
        _matings.Clear();
        return taken;
    }

    /// <summary>A pet is going away (removed): any courtship it's in is off.</summary>
    public void Forget(int petId, IReadOnlyList<BreedingPet> pets)
    {
        ArgumentNullException.ThrowIfNull(pets);
        if (_courting is { } c && (c.A == petId || c.B == petId))
        {
            CallOff(c, pets);
        }
    }

    /// <summary>Whether one pet could mate now, apart from who's near it.</summary>
    internal static bool Ready(PetSimulation sim, BreedingRules rules, DateTimeOffset now)
    {
        var cooldown = TimeSpan.FromHours(rules.CooldownHours);
        return !sim.Traits.Fixed && !sim.UserAway && sim.CanCourt && sim.Stats.Lowest >= rules.MinStat
            && (sim.LastMatedUtc is not { } last || now - last >= cooldown);
    }

    /// <summary>On the same surface with their feet within <see cref="BreedingRules.ReachDip"/>.</summary>
    private static bool Close(PetSimulation a, PetSimulation b, BreedingRules rules)
    {
        if (a.GroundKey is not { } ground || ground != b.GroundKey)
        {
            return false;
        }

        var reach = rules.ReachDip * Scale(a);
        return System.Math.Abs(a.Current.FeetPos.X - b.Current.FeetPos.X) <= reach;
    }

    private static double Scale(PetSimulation sim) => sim.Current.Size.Y / System.Math.Max(1, sim.Config.SizeDip);

    private void Start(BreedingPet a, BreedingPet b, BreedingRules rules)
    {
        var (left, right) = a.Sim.Current.FeetPos.X <= b.Sim.Current.FeetPos.X ? (a, b) : (b, a);
        var middle = (left.Sim.Current.FeetPos.X + right.Sim.Current.FeetPos.X) / 2;
        var half = left.Sim.Current.Size.Y * 0.35; // close enough that they touch noses
        var giveUp = rules.ApproachSeconds + rules.CourtshipSeconds + 5;
        left.Sim.Court(middle - half, faceLeft: false, giveUp);
        right.Sim.Court(middle + half, faceLeft: true, giveUp);
        _courting = new Courting(left.Id, right.Id);
    }

    private void Continue(Courting courting, double dt, IReadOnlyList<BreedingPet> pets, BreedingRules rules)
    {
        var a = pets.FirstOrDefault(p => p.Id == courting.A)?.Sim;
        var b = pets.FirstOrDefault(p => p.Id == courting.B)?.Sim;
        if (a is not { IsCourting: true } || b is not { IsCourting: true })
        {
            CallOff(courting, pets);
            return;
        }

        courting.Elapsed += dt;
        if (!(a.CourtshipArrived && b.CourtshipArrived))
        {
            if (courting.Elapsed > rules.ApproachSeconds)
            {
                CallOff(courting, pets);
            }

            return;
        }

        courting.Together += dt;
        if (courting.Together < rules.CourtshipSeconds)
        {
            return;
        }

        var (fa, fb) = (a.Current.FeetPos, b.Current.FeetPos);
        var lift = a.Current.Size.Y * 0.4;
        _matings.Add(new Mating(courting.A, courting.B, new Vec2((fa.X + fb.X) / 2, System.Math.Min(fa.Y, fb.Y) - lift)));
        a.EndCourtship(mated: true);
        b.EndCourtship(mated: true);
        _courting = null;
    }

    private void CallOff(Courting courting, IReadOnlyList<BreedingPet> pets)
    {
        foreach (var pet in pets.Where(p => p.Id == courting.A || p.Id == courting.B))
        {
            pet.Sim.EndCourtship(mated: false);
        }

        _courting = null;
    }

    private sealed class Courting(int a, int b)
    {
        public int A { get; } = a;

        public int B { get; } = b;

        /// <summary>Seconds since it started.</summary>
        public double Elapsed { get; set; }

        /// <summary>Seconds both have been next to each other.</summary>
        public double Together { get; set; }
    }
}

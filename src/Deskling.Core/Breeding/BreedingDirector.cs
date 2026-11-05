using Deskling.Core.Abstractions;
using Deskling.Core.Behavior;
using Deskling.Core.Math;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;

namespace Deskling.Core.Breeding;

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
/// <para>
/// Breeding pills (M15): a pet that swallowed one goes round every other pet, nearest first, walking (and climbing)
/// up to each and asking. One that could mate anyway says yes with <see cref="BreedingRules.PillAgreeChance"/> (scaled
/// by its Mating trait), and the two court as above; the pill overrides the asker's own cooldown and stats. What they
/// say comes out of <see cref="TakeSayings"/>.
/// </para>
/// </summary>
public sealed class BreedingDirector(IRandom random, IClock clock)
{
    private readonly IRandom _random = random ?? throw new ArgumentNullException(nameof(random));
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly List<Mating> _matings = [];
    private readonly List<Saying> _sayings = [];
    private readonly List<Round> _rounds = [];
    private Courting? _courting;

    /// <summary>The pets on a pill round now, and who each is asking next (null while it picks).</summary>
    public IReadOnlyList<(int Asker, int? Target)> PillRounds => [.. _rounds.Select(r => (r.Asker, r.Target))];

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
        var wasCourting = _courting is not null;
        if (_courting is { } courting)
        {
            Continue(courting, dt, pets, rules);
        }

        TickRounds(dt, pets, rules, roomForEgg);
        if (wasCourting || _courting is not null)
        {
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

    /// <summary>What the pets said on pill rounds since the last call; the host shows them as speech bubbles.</summary>
    public IReadOnlyList<Saying> TakeSayings()
    {
        if (_sayings.Count == 0)
        {
            return [];
        }

        var taken = _sayings.ToArray();
        _sayings.Clear();
        return taken;
    }

    /// <summary>A pet is going away (removed): any courtship it's in is off, its pill round ends, nobody asks it.</summary>
    public void Forget(int petId, IReadOnlyList<BreedingPet> pets)
    {
        ArgumentNullException.ThrowIfNull(pets);
        if (_courting is { } c && (c.A == petId || c.B == petId))
        {
            CallOff(c, pets);
        }

        _rounds.RemoveAll(r => r.Asker == petId);
        foreach (var round in _rounds.Where(r => r.Target == petId))
        {
            round.Asked.Add(petId);
            round.Target = null;
            round.AskSeconds = null;
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

    // ---- Pill rounds ----

    private void TickRounds(double dt, IReadOnlyList<BreedingPet> pets, BreedingRules rules, bool roomForEgg)
    {
        foreach (var pet in pets.Where(p => p.Sim.OnPillRound && _rounds.All(r => r.Asker != p.Id)))
        {
            _rounds.Add(new Round(pet.Id));
        }

        var now = _clock.UtcNow;
        foreach (var round in _rounds.ToList())
        {
            var asker = pets.FirstOrDefault(p => p.Id == round.Asker);
            if (asker is null || !asker.Sim.OnPillRound)
            {
                _rounds.Remove(round); // gone, or the round was ended elsewhere
                continue;
            }

            if (InCourtship(round.Asker))
            {
                round.Courted = true; // wait for it to finish
                continue;
            }

            if (round.Courted)
            {
                round.Courted = false;
                NextTarget(round, asker);
            }

            if (!rules.Enabled || asker.Sim.Traits.Fixed || !roomForEgg)
            {
                End(round, asker);
                continue;
            }

            if (asker.Sim.UserAway)
            {
                continue; // nobody to watch: the round waits
            }

            round.Elapsed += dt;
            if (round.Elapsed > rules.PillMinutes * 60)
            {
                End(round, asker);
                continue;
            }

            if (round.Target is null)
            {
                var next = pets
                    .Where(p => p.Id != round.Asker && !round.Asked.Contains(p.Id))
                    .OrderBy(p => (p.Sim.Current.FeetPos - asker.Sim.Current.FeetPos).Length)
                    .FirstOrDefault();
                if (next is null)
                {
                    End(round, asker); // asked everyone
                    continue;
                }

                round.Target = next.Id;
                round.TargetSeconds = 0;
                round.AskSeconds = null;
            }

            var target = pets.FirstOrDefault(p => p.Id == round.Target);
            round.TargetSeconds += dt;
            if (target is null || target.Sim.Pen is not null || round.TargetSeconds > rules.PillVisitSeconds
                || (round.AskSeconds is null && asker.Sim.PillRoundBlocked))
            {
                NextTarget(round, asker); // gone, shut in, or out of reach: on to the next
                continue;
            }

            if (round.AskSeconds is { } asked)
            {
                Asking(round, asked + dt, asker, target, rules, now);
            }
            else
            {
                Approach(round, asker, target);
            }
        }
    }

    /// <summary>Walks up beside the target (on its current side of it), and asks once there.</summary>
    private void Approach(Round round, BreedingPet asker, BreedingPet target)
    {
        var (a, t) = (asker.Sim, target.Sim);
        if (t.Mode != BodyMode.Grounded || t.GroundKey is not { } ground)
        {
            return; // in the air, climbing or held: keep heading for where it was
        }

        var (ax, tx) = (a.Current.FeetPos.X, t.Current.FeetPos.X);
        var size = t.Current.Size.Y;
        if (a.Mode == BodyMode.Grounded && a.GroundKey == ground && System.Math.Abs(ax - tx) <= size * 1.2 && !t.IsCourting)
        {
            a.AskToMate(faceLeft: tx < ax);
            t.Listen(faceLeft: ax < tx);
            round.AskSeconds = 0;
            _sayings.Add(new Saying(asker.Id, PillLines.Ask(_random, t.Traits.Name)));
            return;
        }

        var side = ax <= tx ? -1 : 1;
        a.Seek(ground, tx + (side * size * 0.8));
    }

    /// <summary>Asking: the target stays to listen, then answers once the ask has lasted long enough.</summary>
    private void Asking(Round round, double seconds, BreedingPet asker, BreedingPet target, BreedingRules rules, DateTimeOffset now)
    {
        round.AskSeconds = seconds;
        var (a, t) = (asker.Sim, target.Sim);
        var listening = t.Mode == BodyMode.Grounded && t.GroundKey == a.GroundKey && !t.IsCourting;
        if (!listening || a.Mode != BodyMode.Grounded)
        {
            round.AskSeconds = null; // it was picked up or walked off: go after it again
            a.Seek(null, 0);
            return;
        }

        var faceAsker = a.Current.FeetPos.X < t.Current.FeetPos.X;
        if (t.State != PetStateId.Idle)
        {
            t.Listen(faceAsker); // stay put (an idle spell can run out while it's asked)
        }

        if (seconds < rules.PillAskSeconds || _courting is not null)
        {
            return; // let it finish asking (and wait while another pair courts)
        }

        if (Refusal(t, rules, now) is { } reason)
        {
            _sayings.Add(new Saying(target.Id, PillLines.No(_random, reason)));
            t.Refuse(walkLeft: !faceAsker);
            NextTarget(round, asker);
            return;
        }

        _sayings.Add(new Saying(target.Id, PillLines.Yes(_random)));
        a.Seek(null, 0);
        Start(asker, target, rules);
        round.Courted = true;
    }

    /// <summary>
    /// Why an asked pet says no, or null for yes. Another pet on a pill round says yes unless it's fixed; any other
    /// must be able to mate anyway and then agrees by chance.
    /// </summary>
    private NoReason? Refusal(PetSimulation pet, BreedingRules rules, DateTimeOffset now)
    {
        if (pet.Traits.Fixed)
        {
            return NoReason.Fixed;
        }

        if (pet.OnPillRound)
        {
            return null;
        }

        if (pet.LastMatedUtc is { } last && now - last < TimeSpan.FromHours(rules.CooldownHours))
        {
            return NoReason.Cooldown;
        }

        if (pet.Stats.Lowest < rules.MinStat)
        {
            return NoReason.NotWell;
        }

        var chance = System.Math.Min(1, rules.PillAgreeChance * pet.Traits.Behavior.Mating);
        return _random.NextDouble() < chance ? null : NoReason.NotInterested;
    }

    private bool InCourtship(int petId) => _courting is { } c && (c.A == petId || c.B == petId);

    private static void NextTarget(Round round, BreedingPet asker)
    {
        if (round.Target is { } id)
        {
            round.Asked.Add(id);
        }

        round.Target = null;
        round.AskSeconds = null;
        asker.Sim.Seek(null, 0);
    }

    private void End(Round round, BreedingPet asker)
    {
        asker.Sim.EndPillRound();
        _rounds.Remove(round);
    }

    /// <summary>One pet's pill round.</summary>
    private sealed class Round(int asker)
    {
        public int Asker { get; } = asker;

        /// <summary>Pets already asked (or skipped).</summary>
        public HashSet<int> Asked { get; } = [];

        /// <summary>The pet it's on its way to or asking.</summary>
        public int? Target { get; set; }

        /// <summary>Seconds since the round started (not counting time away).</summary>
        public double Elapsed { get; set; }

        /// <summary>Seconds spent on the current target.</summary>
        public double TargetSeconds { get; set; }

        /// <summary>Seconds it has been asking the target, or null while it's still on its way.</summary>
        public double? AskSeconds { get; set; }

        /// <summary>It's courting (or just was) the pet it asked.</summary>
        public bool Courted { get; set; }
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

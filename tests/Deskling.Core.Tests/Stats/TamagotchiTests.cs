using Deskling.Core.Animation;
using Deskling.Core.Behavior;
using Deskling.Core.Math;
using Deskling.Core.Persistence;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;
using Deskling.Core.Stats;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.World;
using static Deskling.Core.Tests.Fakes.SimulationExtensions;

namespace Deskling.Core.Tests.Stats;

/// <summary>Stats inside the running simulation: decay by wall clock, away rules, actions, mood, save/load.</summary>
public class TamagotchiTests
{
    private static readonly DesktopSnapshot Desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
    private static readonly StatRules Rules = StatRules.Default;

    /// <summary>A pet standing on the floor, with a controllable clock and long-lasting idles.</summary>
    private static (PetSimulation Sim, FakeClock Clock) Standing(PetStats? stats = null, double roll = 0.0)
    {
        var clock = new FakeClock();
        var save = stats is { } s ? SaveData.From(s, clock.UtcNow) : null;
        var sim = NewSim(Desktop, new FakeRandom(roll), clock: clock, save: save);
        sim.RunUntil(Desktop, Grounded);
        sim.ForceState(PetStateId.Idle);
        return (sim, clock);
    }

    /// <summary>Lets <paramref name="span"/> of wall-clock time pass in 30 s steps, ticking once per step.</summary>
    private static void LiveFor(PetSimulation sim, FakeClock clock, TimeSpan span)
    {
        for (var t = TimeSpan.Zero; t < span; t += TimeSpan.FromSeconds(30))
        {
            clock.Advance(TimeSpan.FromSeconds(30));
            sim.Tick(Dt, Desktop);
        }
    }

    [Fact]
    public void A_fresh_pet_starts_at_fresh_stats()
    {
        var sim = NewSim(Desktop);
        Assert.Equal(PetStats.Fresh, sim.Stats);
    }

    [Fact]
    public void Stats_decay_with_wall_clock_time_not_ticks()
    {
        var (sim, clock) = Standing(new PetStats(90, 90, 90));
        sim.Run(Desktop, 600); // 10 s of ticks, but the wall clock didn't move
        Assert.Equal(new PetStats(90, 90, 90), sim.Stats);

        LiveFor(sim, clock, TimeSpan.FromHours(1));
        Assert.Equal(90 + Rules.HungerPerHour, sim.Stats.Hunger, 1);
    }

    [Fact]
    public void Present_user_neglect_can_reach_critical()
    {
        var (sim, clock) = Standing(new PetStats(40, 40, 40));
        LiveFor(sim, clock, TimeSpan.FromHours(4));
        Assert.True(sim.Stats.Hunger < Rules.Critical);
    }

    [Fact]
    public void Locked_screen_keeps_every_stat_above_critical()
    {
        var (sim, clock) = Standing(new PetStats(40, 40, 40));
        sim.UserAway = true;

        LiveFor(sim, clock, TimeSpan.FromHours(10));

        Assert.True(sim.Stats.Lowest > Rules.Critical, sim.Stats.ToString());
        Assert.Equal(Rules.AwayFloor, sim.Stats.Hunger);
    }

    [Fact]
    public void Sleeping_pc_gap_counts_as_away()
    {
        var (sim, clock) = Standing(new PetStats(40, 40, 40));

        clock.Advance(TimeSpan.FromHours(10)); // process frozen: no ticks for 10 hours
        sim.Tick(Dt, Desktop);

        Assert.True(sim.Stats.Lowest > Rules.Critical, sim.Stats.ToString());
    }

    [Fact]
    public void Loading_a_week_old_save_catches_up_without_reaching_critical()
    {
        var clock = new FakeClock();
        var save = SaveData.From(new PetStats(60, 60, 60), clock.UtcNow - TimeSpan.FromDays(7));

        var sim = NewSim(Desktop, clock: clock, save: save);

        Assert.Equal(Rules.AwayFloor, sim.Stats.Hunger);
        Assert.Equal(Rules.AwayFloor, sim.Stats.Happiness);
        Assert.Equal(100, sim.Stats.Energy); // it napped the whole time
    }

    [Fact]
    public void Save_round_trips_through_the_simulation()
    {
        var (sim, clock) = Standing(new PetStats(55, 66, 77));
        var save = sim.CreateSave();

        Assert.Equal(new PetStats(55, 66, 77), save.Stats);
        Assert.Equal(clock.UtcNow, save.SavedAtUtc);

        var reloaded = NewSim(Desktop, clock: clock, save: save);
        Assert.Equal(new PetStats(55, 66, 77), reloaded.Stats);
    }

    // ---- Actions ----

    [Fact]
    public void Feed_raises_hunger_and_plays_the_eating_behavior()
    {
        var (sim, _) = Standing(new PetStats(30, 50, 50));
        sim.Apply(PetAction.Feed);

        Assert.Equal(30 + Rules.FeedHunger, sim.Stats.Hunger);
        Assert.Equal(PetStateId.Eat, sim.State);
        sim.Tick(Dt, Desktop);
        Assert.Equal(Animations.Eat.Id, sim.Current.AnimationId);

        sim.RunSeconds(Desktop, PetConfig.Default.EatSeconds + 0.1);
        Assert.NotEqual(PetStateId.Eat, sim.State);
    }

    [Fact]
    public void Play_hops_a_few_times_without_the_landing_squash()
    {
        var (sim, _) = Standing(new PetStats(50, 40, 50));
        var floorY = sim.Current.FeetPos.Y;
        sim.Apply(PetAction.Play);
        Assert.Equal(40 + Rules.PlayHappiness, sim.Stats.Happiness);

        var takeoffs = 0;
        var wasGrounded = true;
        var states = new HashSet<PetStateId>();
        for (var i = 0; i < 60 * 5 && sim.State == PetStateId.Play; i++)
        {
            sim.Tick(Dt, Desktop);
            states.Add(sim.State);
            var grounded = sim.Mode == BodyMode.Grounded;
            if (wasGrounded && !grounded)
            {
                takeoffs++;
            }

            wasGrounded = grounded;
        }

        Assert.Equal(PetConfig.Default.PlayHops, takeoffs);
        Assert.DoesNotContain(PetStateId.Land, states);
        Assert.NotEqual(PetStateId.Play, sim.State); // finished
        Assert.Equal(floorY, sim.Current.FeetPos.Y);
    }

    [Fact]
    public void Tucked_in_pet_sleeps_until_rested_then_wakes()
    {
        var (sim, clock) = Standing(new PetStats(80, 80, 30));
        sim.Apply(PetAction.Sleep);
        Assert.Equal(PetStateId.Sleep, sim.State);

        LiveFor(sim, clock, TimeSpan.FromMinutes(30));
        Assert.Equal(PetStateId.Sleep, sim.State); // 30 min at 45/h ≈ +22: still not rested
        Assert.True(sim.Stats.Energy > 50);

        // Wakes by itself on reaching WellRested (~1 h in), then stays awake, so energy drifts down a little after.
        var wokeWith = 0.0;
        for (var t = TimeSpan.Zero; t < TimeSpan.FromHours(1.5) && sim.State == PetStateId.Sleep; t += TimeSpan.FromSeconds(30))
        {
            clock.Advance(TimeSpan.FromSeconds(30));
            sim.Tick(Dt, Desktop);
            wokeWith = sim.Stats.Energy;
        }

        Assert.NotEqual(PetStateId.Sleep, sim.State);
        Assert.True(wokeWith >= Rules.WellRested, $"woke at {wokeWith:F1}");
    }

    [Fact]
    public void Clicking_a_sleeping_pet_wakes_it_instead_of_petting()
    {
        var (sim, _) = Standing(new PetStats(80, 50, 30));
        sim.Apply(PetAction.Sleep);

        sim.PointerDown(sim.Current.FeetPos - new Vec2(0, 20), 1);
        sim.PointerUp(sim.Current.FeetPos - new Vec2(0, 20), 1.05);

        Assert.Equal(PetStateId.Idle, sim.State);
        Assert.Equal(50, sim.Stats.Happiness);
    }

    [Fact]
    public void Petting_raises_happiness()
    {
        var (sim, _) = Standing(new PetStats(80, 50, 80));
        sim.PointerDown(sim.Current.FeetPos - new Vec2(0, 20), 1);
        sim.PointerUp(sim.Current.FeetPos - new Vec2(0, 20), 1.05);
        Assert.Equal(50 + Rules.PetHappiness, sim.Stats.Happiness);
    }

    [Fact]
    public void Feeding_a_falling_pet_changes_stats_without_interrupting_the_fall()
    {
        var sim = NewSim(Desktop, new FakeRandom(0.0));
        sim.Tick(Dt, Desktop);
        sim.Apply(PetAction.Feed);
        Assert.Equal(PetStateId.Fall, sim.State);
        Assert.Equal(System.Math.Min(100, PetStats.Fresh.Hunger + Rules.FeedHunger), sim.Stats.Hunger);
    }

    // ---- Mood ----

    [Fact]
    public void Sad_pet_idles_with_a_sad_face()
    {
        var (sim, _) = Standing(new PetStats(80, Rules.Sad - 5, 80));
        sim.ForceState(PetStateId.Idle);
        sim.Tick(Dt, Desktop);
        Assert.Equal(Animations.IdleSad.Id, sim.Current.AnimationId);
    }

    [Fact]
    public void Happy_pet_idles_normally()
    {
        var (sim, _) = Standing(new PetStats(80, 80, 80));
        sim.ForceState(PetStateId.Idle);
        sim.Tick(Dt, Desktop);
        Assert.Equal(Animations.Idle.Id, sim.Current.AnimationId);
    }

    [Fact]
    public void Healthy_pet_never_chooses_to_sleep()
    {
        var w = BehaviorPicker.Weights(PetConfig.Default, new PetStats(80, 80, 80));
        Assert.Equal(0, w.Sleep);
    }

    [Fact]
    public void Exhausted_pet_mostly_sleeps_and_barely_walks()
    {
        var w = BehaviorPicker.Weights(PetConfig.Default, new PetStats(80, 80, 5));
        var total = w.Idle + w.Walk + w.Sit + w.Sleep;
        Assert.True(w.Sleep / total > 0.4, $"sleep share {w.Sleep / total:P0}");
        Assert.True(w.Walk < PetConfig.Default.Weights.Walk * 0.3);
    }

    [Fact]
    public void Sad_pet_sits_more_and_walks_less()
    {
        var happy = BehaviorPicker.Weights(PetConfig.Default, new PetStats(80, 80, 80));
        var sad = BehaviorPicker.Weights(PetConfig.Default, new PetStats(80, 10, 80));
        Assert.True(sad.Sit > happy.Sit);
        Assert.True(sad.Walk < happy.Walk);
    }

    [Fact]
    public void Tired_pet_naps_by_itself_and_wakes_rested()
    {
        // Every roll 0.99: the Land pick lands in the sleep band (weights at energy 10 put sleep last and largest).
        var clock = new FakeClock();
        var sim = NewSim(Desktop, new FakeRandom(0.99), clock: clock, save: SaveData.From(new PetStats(80, 80, 10), clock.UtcNow));
        sim.RunUntil(Desktop, s => s.State == PetStateId.Sleep, maxTicks: 200);
        Assert.Equal(Animations.Sleep.Id, sim.Current.AnimationId);
    }
}

using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Tests.Fakes;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Behavior;

public class BehaviorTests
{
    private static readonly Core.World.DesktopSnapshot Desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();

    [Theory]
    [InlineData(0.0, PetStateId.Idle)]   // weights 3 / 5 / 1.5, total 9.5
    [InlineData(0.31, PetStateId.Idle)]  // 2.95
    [InlineData(0.32, PetStateId.Walk)]  // 3.04
    [InlineData(0.84, PetStateId.Walk)]  // 7.98
    [InlineData(0.85, PetStateId.Sit)]   // 8.08
    [InlineData(0.999, PetStateId.Sit)]
    public void Picker_maps_roll_to_weighted_behavior(double roll, PetStateId expected) =>
        Assert.Equal(expected, BehaviorPicker.Pick(PetConfig.Default, new FakeRandom(roll), Core.Stats.PetStats.Fresh));

    [Fact]
    public void Picker_mostly_sits_when_penned()
    {
        var random = new SeededRandom(3);
        var sits = 0;
        for (var i = 0; i < 1000; i++)
        {
            sits += BehaviorPicker.Pick(PetConfig.Default, random, Core.Stats.PetStats.Fresh, penned: true) == PetStateId.Sit ? 1 : 0;
        }

        Assert.InRange(sits, 580, 720); // 15 of 23 ≈ 65% of picks (and sits last longer than the rest)
    }

    [Fact]
    public void Picker_never_picks_zero_weight_behavior()
    {
        var config = PetConfig.Default with { Weights = new BehaviorWeights(Idle: 0, Walk: 1, Sit: 0) };
        var random = new SeededRandom(1);
        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(PetStateId.Walk, BehaviorPicker.Pick(config, random, Core.Stats.PetStats.Fresh));
        }
    }

    [Fact]
    public void Timed_state_lasts_its_rolled_duration_then_picks_next()
    {
        // Every roll is 0.5: Idle lasts 2 + 0.5 × 3 = 3.5 s; then the pick (0.5 × 9.5 = 4.75) is Walk.
        var sim = NewSim(Desktop, new FakeRandom(0.5));
        sim.RunUntil(Desktop, Grounded);
        sim.ForceState(PetStateId.Idle);

        sim.RunSeconds(Desktop, 3.4);
        Assert.Equal(PetStateId.Idle, sim.State);
        sim.RunSeconds(Desktop, 0.2);
        Assert.Equal(PetStateId.Walk, sim.State);
    }

    [Theory]
    [InlineData(0.0, true)]   // roll < WalkTurnChance (0.5) → turns around
    [InlineData(0.99, false)] // keeps heading
    public void Walk_may_turn_on_start(double roll, bool expectFacingLeft)
    {
        var sim = NewSim(Desktop, new FakeRandom(roll));
        sim.RunUntil(Desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft: false);
        sim.Tick(Dt, Desktop);
        Assert.Equal(expectFacingLeft, sim.Current.FacingLeft);
    }

    [Fact]
    public void Sit_plays_sit_animation_and_stays_put()
    {
        var sim = NewSim(Desktop, new FakeRandom(0.99));
        sim.RunUntil(Desktop, Grounded);
        sim.ForceState(PetStateId.Sit);
        var x = sim.Current.FeetPos.X;

        sim.RunSeconds(Desktop, 2);

        Assert.Equal(PetStateId.Sit, sim.State);
        Assert.Equal(Core.Animation.Animations.Sit.Id, sim.Current.AnimationId);
        Assert.Equal(x, sim.Current.FeetPos.X);
    }

    [Fact]
    public void Same_seed_same_life_different_seed_different_life()
    {
        static List<Vec2> Life(int seed)
        {
            var sim = NewSim(Desktop, new SeededRandom(seed));
            var path = new List<Vec2>();
            for (var i = 0; i < 60 * 60; i++) // one simulated minute
            {
                sim.Tick(Dt, Desktop);
                path.Add(sim.Current.FeetPos);
            }

            return path;
        }

        Assert.Equal(Life(42), Life(42));
        Assert.NotEqual(Life(42), Life(43));
    }

    [Fact]
    public void A_minute_of_wandering_stays_on_screen_and_uses_every_behavior()
    {
        var sim = NewSim(Desktop, new SeededRandom(7));
        var seen = new HashSet<PetStateId>();
        for (var i = 0; i < 60 * 60 * 5; i++)
        {
            sim.Tick(Dt, Desktop);
            seen.Add(sim.State);
            Assert.InRange(sim.Current.FeetPos.X, 0, 1920);
        }

        Assert.Superset(new HashSet<PetStateId> { PetStateId.Idle, PetStateId.Walk, PetStateId.Sit }, seen);
        Assert.Equal(0, sim.RespawnCount);
    }
}

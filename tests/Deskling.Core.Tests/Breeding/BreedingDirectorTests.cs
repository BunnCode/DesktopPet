using Deskling.Core.Behavior;
using Deskling.Core.Breeding;
using Deskling.Core.Math;
using Deskling.Core.Persistence;
using Deskling.Core.Simulation;
using Deskling.Core.Stats;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.Traits;
using Deskling.Core.World;
using static Deskling.Core.Tests.Fakes.SimulationExtensions;

namespace Deskling.Core.Tests.Breeding;

/// <summary>When pets mate: close, fine, free, not fixed, past the cooldown; the courtship and the egg.</summary>
public class BreedingDirectorTests
{
    private static readonly BreedingRules Rules = new();

    private sealed class Scene
    {
        public Scene(double xa = 900, double xb = 1000, double? rate = null, SaveData? saveA = null)
        {
            Desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Snapshot(); // floor at 1040
            A = NewSim(Desktop, clock: Clock, save: saveA);
            B = NewSim(Desktop, clock: Clock);
            A.SetTraits(PetTraits.Neutral("Mia"));
            B.SetTraits(PetTraits.Neutral("Leo"));
            A.Place(new Vec2(xa, 1000), Vec2.Zero);
            B.Place(new Vec2(xb, 1000), Vec2.Zero);
            Rules = BreedingDirectorTests.Rules with { MatesPerHour = rate ?? BreedingDirectorTests.Rules.MatesPerHour };
            Settle();
        }

        public FakeClock Clock { get; } = new();

        public DesktopSnapshot Desktop { get; }

        public PetSimulation A { get; }

        public PetSimulation B { get; }

        public BreedingRules Rules { get; set; }

        public bool RoomForEgg { get; set; } = true;

        public BreedingDirector Director { get; } = new(new FakeRandom(0.0), new FakeClock());

        public List<Mating> Matings { get; } = [];

        public IReadOnlyList<BreedingPet> Pets => [new(1, A), new(2, B)];

        /// <summary>Both land and stand idle (FakeRandom(0.0) always picks Idle).</summary>
        public void Settle()
        {
            for (var i = 0; i < 120; i++)
            {
                A.Tick(Dt, Desktop);
                B.Tick(Dt, Desktop);
            }

            Assert.True(A.CanCourt && B.CanCourt, $"A {A.State}, B {B.State}");
        }

        public void Tick()
        {
            A.Tick(Dt, Desktop);
            B.Tick(Dt, Desktop);
            Director.Tick(Dt, Pets, Rules, RoomForEgg);
            Matings.AddRange(Director.TakeMatings());
        }

        public void RunSeconds(double seconds)
        {
            for (var i = 0; i < seconds / Dt; i++)
            {
                Tick();
            }
        }
    }

    // The director's own clock is frozen at the same time as the scene's, so cooldowns are measured from the scene's
    // LastMated values (both FakeClocks start at the same moment).

    [Fact]
    public void Two_happy_pets_close_together_court_and_lay_an_egg_between_them()
    {
        var scene = new Scene();
        scene.Tick();
        Assert.Equal(PetStateId.Court, scene.A.State);
        Assert.Equal(PetStateId.Court, scene.B.State);
        Assert.Equal((1, 2), scene.Director.Courtship);

        scene.RunSeconds(4); // walking up to each other
        Assert.True(scene.A.CourtshipArrived && scene.B.CourtshipArrived);
        Assert.Equal("love", scene.A.Current.AnimationId);
        Assert.False(scene.A.Current.FacingLeft); // the left one faces right, towards its partner
        Assert.True(scene.B.Current.FacingLeft);
        Assert.Empty(scene.Matings);

        scene.RunSeconds(Rules.CourtshipSeconds + 0.5);
        var mating = Assert.Single(scene.Matings);
        Assert.Equal(1, mating.ParentA);
        Assert.Equal(2, mating.ParentB);
        Assert.Equal(950, mating.EggFeet.X, 1);
        Assert.True(mating.EggFeet.Y < 1040);
        Assert.Null(scene.Director.Courtship);
        Assert.Equal(scene.Clock.UtcNow, scene.A.LastMatedUtc);
        Assert.Equal(scene.Clock.UtcNow, scene.B.LastMatedUtc);
        Assert.NotEqual(PetStateId.Court, scene.A.State);
    }

    [Fact]
    public void Mated_pets_wait_out_the_cooldown_which_survives_a_save()
    {
        var scene = new Scene();
        scene.RunSeconds(15);
        Assert.Single(scene.Matings);

        scene.RunSeconds(2);
        scene.Settle();
        scene.RunSeconds(5);
        Assert.Single(scene.Matings); // no second egg

        var save = scene.A.CreateSave();
        Assert.Equal(scene.Clock.UtcNow, save.LastMatedUtc);
    }

    [Fact]
    public void A_pet_that_mated_within_the_cooldown_before_a_restart_doesnt_court()
    {
        var clock = new FakeClock();
        var save = SaveData.From(PetStats.Fresh, clock.UtcNow, lastMated: clock.UtcNow - TimeSpan.FromHours(Rules.CooldownHours - 1));
        var scene = new Scene(saveA: save);
        scene.Tick();
        Assert.Null(scene.Director.Courtship);
    }

    [Fact]
    public void Pets_far_apart_dont_court()
    {
        var scene = new Scene(xa: 300, xb: 1500);
        scene.RunSeconds(1);
        Assert.Null(scene.Director.Courtship);
    }

    [Fact]
    public void A_fixed_pet_never_courts()
    {
        var scene = new Scene();
        scene.B.SetTraits(scene.B.Traits with { Fixed = true });
        scene.RunSeconds(1);
        Assert.Null(scene.Director.Courtship);
    }

    [Fact]
    public void Nothing_happens_when_switched_off_at_the_pet_cap_or_with_low_stats()
    {
        var off = new Scene();
        off.Rules = Rules with { Enabled = false };
        off.RunSeconds(1);
        Assert.Null(off.Director.Courtship);

        var full = new Scene { RoomForEgg = false };
        full.RunSeconds(1);
        Assert.Null(full.Director.Courtship);

        var picky = new Scene();
        picky.Rules = Rules with { MinStat = 90 }; // fresh pets are at 80
        picky.RunSeconds(1);
        Assert.Null(picky.Director.Courtship);
    }

    [Fact]
    public void Picking_one_up_calls_the_courtship_off_and_no_egg_is_laid()
    {
        var scene = new Scene();
        scene.RunSeconds(1);
        Assert.NotNull(scene.Director.Courtship);

        var feet = scene.A.Current.FeetPos;
        scene.A.PointerDown(feet - new Vec2(0, 20), 0);
        scene.A.PointerMove(feet - new Vec2(0, 60), 0.05);
        scene.RunSeconds(0.2);

        Assert.Null(scene.Director.Courtship);
        Assert.NotEqual(PetStateId.Court, scene.B.State);
        Assert.Null(scene.B.LastMatedUtc); // no cooldown for a courtship that didn't work out
        scene.RunSeconds(10);
        Assert.Empty(scene.Matings);
    }

    [Fact]
    public void A_zero_rate_or_a_zero_mating_trait_means_no_mating()
    {
        var never = new Scene(rate: 0);
        never.RunSeconds(1);
        Assert.Null(never.Director.Courtship);

        var shy = new Scene();
        shy.A.SetTraits(shy.A.Traits with { Behavior = new BehaviorTraits(Mating: 0) });
        shy.RunSeconds(1);
        Assert.Null(shy.Director.Courtship);
    }

    [Fact]
    public void Removing_a_courting_pet_releases_its_partner()
    {
        var scene = new Scene();
        scene.Tick();
        scene.Director.Forget(1, scene.Pets);
        Assert.Null(scene.Director.Courtship);
        Assert.NotEqual(PetStateId.Court, scene.B.State);
    }

    [Fact]
    public void A_pet_stops_courting_by_itself_when_nobody_ends_it()
    {
        var scene = new Scene();
        scene.A.Court(950, faceLeft: false, giveUpSeconds: 2);
        scene.A.RunSeconds(scene.Desktop, 3);
        Assert.NotEqual(PetStateId.Court, scene.A.State);
    }
}

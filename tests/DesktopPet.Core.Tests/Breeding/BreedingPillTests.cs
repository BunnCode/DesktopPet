using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Breeding;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.Traits;
using DesktopPet.Core.World;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Breeding;

/// <summary>M15: the breeding pill. A pet swallows it, then goes round every other pet asking to mate.</summary>
public class BreedingPillTests
{
    private const double Floor = 1040;
    private static readonly PetStats Content = new(90, 90, 90);

    /// <summary>Pets, furniture, the director and one clock, stepped together.</summary>
    private sealed class Room
    {
        public Room(bool window = false, BreedingRules? rules = null)
        {
            var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40);
            Desktop = (window ? desktop.Window(5, 600, 700, 400, 340) : desktop).Snapshot(); // window top at 700, x 600..1000
            World = new FurnitureWorld(PetConfig.Default, DefaultFurniture.All);
            Rules = rules ?? new BreedingRules { MatesPerHour = 0 }; // no chance matings: only the pill's
            Director = new BreedingDirector(new FakeRandom(0.0), Clock);
        }

        public DesktopSnapshot Desktop { get; }

        public FurnitureWorld World { get; }

        public FakeClock Clock { get; } = new();

        public BreedingRules Rules { get; set; }

        public BreedingDirector Director { get; }

        public bool RoomForEgg { get; set; } = true;

        public List<BreedingPet> Pets { get; } = [];

        public List<Mating> Matings { get; } = [];

        public List<Saying> Sayings { get; } = [];

        public FurnitureItem Pill(double x)
        {
            var item = World.Add("pill", new Vec2(x, Floor - 100))!;
            for (var i = 0; i < 120 && !item.IsGrounded; i++)
            {
                World.Tick(Dt, Desktop);
            }

            Assert.True(item.IsGrounded);
            return item;
        }

        /// <summary>
        /// A pet dropped at (<paramref name="x"/>, <paramref name="y"/>). <paramref name="greedy"/> picks furniture
        /// whenever it's weighted (FakeRandom 0.99); otherwise it just idles (0.0).
        /// </summary>
        public PetSimulation Pet(string name, double x, double y = Floor - 10, bool greedy = false, bool isFixed = false)
        {
            IRandom random = greedy ? new FakeRandom(0.99) : new FakeRandom(0.0);
            var sim = NewSim(Desktop, random, PetConfig.Default, Clock, SaveData.From(Content, Clock.UtcNow), World);
            sim.SetTraits(PetTraits.Neutral(name) with { Fixed = isFixed });
            sim.Place(new Vec2(x, y), Vec2.Zero);
            Pets.Add(new BreedingPet(Pets.Count + 1, sim));
            return sim;
        }

        public void Tick()
        {
            World.Tick(Dt, Desktop);
            Clock.Advance(TimeSpan.FromSeconds(Dt));
            foreach (var pet in Pets)
            {
                pet.Sim.Tick(Dt, Desktop);
            }

            Director.Tick(Dt, Pets, Rules, RoomForEgg);
            Matings.AddRange(Director.TakeMatings());
            Sayings.AddRange(Director.TakeSayings());
        }

        public void RunUntil(Func<bool> until, double maxSeconds = 300)
        {
            for (var i = 0; i < maxSeconds / Dt; i++)
            {
                Tick();
                if (until())
                {
                    return;
                }
            }

            Assert.Fail("Not reached. " + string.Join("; ", Pets.Select(p => $"{p.Sim.Traits.Name} {p.Sim.State} {p.Sim.Current.FeetPos} {p.Sim.GroundKey}")));
        }

        public void RunSeconds(double seconds)
        {
            for (var i = 0; i < seconds / Dt; i++)
            {
                Tick();
            }
        }
    }

    [Fact]
    public void A_pet_goes_for_the_pill_swallows_it_and_it_is_gone()
    {
        var room = new Room();
        var pill = room.Pill(500);
        var mia = room.Pet("Mia", 300, greedy: true);
        room.Pet("Leo", 1500);

        room.RunUntil(() => mia.State == PetStateId.UseFurniture, 60);
        Assert.Same(pill, mia.FurnitureTarget);
        Assert.False(mia.OnPillRound);

        room.RunUntil(() => mia.OnPillRound, 10);
        Assert.True(pill.IsRemoved);
        Assert.DoesNotContain(pill, room.World.Items);
        Assert.Equal([(1, (int?)2)], room.Director.PillRounds);
    }

    [Fact]
    public void It_asks_each_other_pet_nearest_first_climbing_to_reach_one_and_mates_with_each_that_agrees()
    {
        var room = new Room(window: true);
        room.Pill(300);
        var mia = room.Pet("Mia", 200, greedy: true);
        var cleo = room.Pet("Cleo", 800, y: 690);  // on the window top: the nearer one, up a climb
        var leo = room.Pet("Leo", 1500);            // on the floor, further away
        room.RunSeconds(1);
        Assert.Equal("window:5", cleo.GroundKey);

        room.RunUntil(() => room.Matings.Count == 1);
        Assert.Equal([1, 2], new[] { room.Matings[0].ParentA, room.Matings[0].ParentB }.Order());
        Assert.Equal("window:5", mia.GroundKey); // it climbed up to Cleo

        room.RunUntil(() => room.Matings.Count == 2);
        Assert.Equal([1, 3], new[] { room.Matings[1].ParentA, room.Matings[1].ParentB }.Order());

        room.RunUntil(() => !mia.OnPillRound, 30);
        Assert.Empty(room.Director.PillRounds);
        Assert.Contains(room.Sayings, s => s.PetId == 1 && s.Text.Contains("Cleo", StringComparison.Ordinal));
        Assert.Contains(room.Sayings, s => s.PetId == 1 && s.Text.Contains("Leo", StringComparison.Ordinal));
        Assert.Equal(2, room.Sayings.Count(s => s.PetId != 1)); // both answered
        Assert.NotNull(leo.LastMatedUtc);
    }

    [Fact]
    public void A_pet_that_says_no_walks_off_and_a_fixed_one_says_so()
    {
        var room = new Room(rules: new BreedingRules { MatesPerHour = 0, PillAgreeChance = 0 });
        room.Pill(300);
        var mia = room.Pet("Mia", 200, greedy: true);
        var leo = room.Pet("Leo", 700);
        room.Pet("Cleo", 1300, isFixed: true);

        room.RunUntil(() => room.Sayings.Count(s => s.PetId == 2) == 1, 120);
        room.RunSeconds(0.2);
        Assert.Equal(PetStateId.Walk, leo.State);
        Assert.True(leo.Current.FacingLeft == leo.Current.FeetPos.X < mia.Current.FeetPos.X); // walking away from Mia

        room.RunUntil(() => !mia.OnPillRound, 120);
        Assert.Empty(room.Matings);
        Assert.Contains(room.Sayings, s => s.PetId == 3 && s.Text == "Sorry, I'm fixed.");
        Assert.Null(leo.LastMatedUtc);
    }

    [Fact]
    public void The_pill_overrides_the_takers_cooldown_but_not_the_one_asked()
    {
        var room = new Room();
        room.Pill(300);
        var mia = room.Pet("Mia", 200, greedy: true);
        var leo = room.Pet("Leo", 700);
        mia.EndCourtship(mated: true); // both mated just now
        leo.EndCourtship(mated: true);

        room.RunUntil(() => !mia.OnPillRound && room.Sayings.Count >= 2, 120);
        Assert.Empty(room.Matings);
        Assert.Contains(room.Sayings, s => s.PetId == 2 && s.Text == "I just had babies!");
    }

    [Fact]
    public void Two_pets_who_both_took_a_pill_say_yes_to_each_other()
    {
        var room = new Room(rules: new BreedingRules { MatesPerHour = 0, PillAgreeChance = 0 });
        room.Pill(300);
        room.Pill(1300);
        var mia = room.Pet("Mia", 200, greedy: true);
        var leo = room.Pet("Leo", 1400, greedy: true);

        room.RunUntil(() => room.Matings.Count == 1, 120);
        room.RunUntil(() => !mia.OnPillRound && !leo.OnPillRound, 120);
        Assert.Single(room.Matings);
    }

    [Fact]
    public void Fixed_pets_and_pets_with_breeding_off_leave_the_pill_alone()
    {
        var room = new Room();
        var pill = room.Pill(300);
        var mia = room.Pet("Mia", 200, greedy: true, isFixed: true);
        room.Pet("Leo", 700);

        room.RunSeconds(20);
        Assert.False(pill.IsRemoved);
        Assert.NotEqual(pill, mia.FurnitureTarget);
        Assert.False(mia.OnPillRound);
    }

    [Fact]
    public void The_round_ends_at_the_pet_cap_and_when_a_pet_is_removed_nobody_asks_it()
    {
        var room = new Room();
        room.Pill(300);
        var mia = room.Pet("Mia", 200, greedy: true);
        room.Pet("Leo", 1500);
        room.RunUntil(() => mia.OnPillRound, 60);

        room.Director.Forget(2, room.Pets);
        room.Pets.RemoveAt(1);
        room.RunUntil(() => !mia.OnPillRound, 5); // nobody left to ask
        Assert.Empty(room.Sayings);

        var full = new Room { RoomForEgg = false };
        var pill = full.Pill(300);
        var kit = full.Pet("Kit", 200, greedy: true);
        full.Pet("Leo", 700);
        full.RunUntil(() => pill.IsRemoved, 60);
        full.RunSeconds(0.2);
        Assert.False(kit.OnPillRound); // swallowed, but there's no room for more pets
        Assert.Empty(full.Sayings);
    }

    [Fact]
    public void Being_picked_up_on_the_way_does_not_end_the_round()
    {
        var room = new Room();
        room.Pill(300);
        var mia = room.Pet("Mia", 200, greedy: true);
        room.Pet("Leo", 1500);
        room.RunUntil(() => mia.State == PetStateId.Visit, 60);

        mia.PointerDown(mia.Current.FeetPos - new Vec2(0, 20), 0);
        mia.PointerMove(mia.Current.FeetPos - new Vec2(0, 80), 0.1);
        room.RunSeconds(0.5);
        Assert.Equal(BodyMode.Held, mia.Mode);
        mia.PointerUp(mia.Current.FeetPos, 2);
        Assert.True(mia.OnPillRound);

        room.RunUntil(() => room.Matings.Count == 1, 120);
    }
}

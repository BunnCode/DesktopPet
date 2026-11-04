using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Behavior;

/// <summary>Pets going to furniture on their own, using it, and being shut in a pen.</summary>
public class FurnitureBehaviorTests
{
    private const long Win = 5;
    private const double Floor = 1040;
    private static readonly PetStats Content = new(90, 90, 90);
    private static readonly PetStats Hungry = new(10, 90, 90);
    private static readonly PetStats Sad = new(90, 10, 90);

    /// <summary>A desktop, its furniture and a wall clock, stepped together.</summary>
    private sealed class Room
    {
        public Room(DesktopSnapshot desktop, PetConfig? config = null)
        {
            Desktop = desktop;
            Config = config ?? PetConfig.Default;
            World = new FurnitureWorld(Config, DefaultFurniture.All);
        }

        public DesktopSnapshot Desktop { get; }

        public PetConfig Config { get; }

        public FurnitureWorld World { get; }

        public FakeClock Clock { get; } = new();

        /// <summary>Drops furniture in and lets it settle.</summary>
        public FurnitureItem Add(string kind, double x, double y = 100)
        {
            var item = World.Add(kind, new Vec2(x, y))!;
            for (var i = 0; i < 120 && !item.IsGrounded; i++)
            {
                World.Tick(Dt, Desktop);
            }

            Assert.True(item.IsGrounded, $"{kind} didn't land");
            return item;
        }

        /// <summary>A pet dropped onto the floor at <paramref name="x"/>; it decides what to do once it has landed.</summary>
        public PetSimulation Pet(PetStats stats, double x, IRandom? random = null)
        {
            var sim = NewSim(Desktop, random ?? new FakeRandom(0.99), Config, Clock, SaveData.From(stats, Clock.UtcNow), World);
            sim.Place(new Vec2(x, Floor - 10), Vec2.Zero);
            return sim;
        }

        public void Tick(PetSimulation sim)
        {
            World.Tick(Dt, Desktop);
            Clock.Advance(TimeSpan.FromSeconds(Dt));
            sim.Tick(Dt, Desktop);
        }

        public void Run(PetSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                Tick(sim);
            }
        }

        public int RunUntil(PetSimulation sim, Func<PetSimulation, bool> until, int maxTicks = 1500)
        {
            for (var tick = 1; tick <= maxTicks; tick++)
            {
                Tick(sim);
                if (until(sim))
                {
                    return tick;
                }
            }

            Assert.Fail($"Not reached in {maxTicks} ticks. Feet={sim.Current.FeetPos}, Mode={sim.Mode}, State={sim.State}, Ground={sim.GroundKey}");
            return -1;
        }
    }

    private static DesktopSnapshot Screen() => new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Snapshot();

    /// <summary>A window standing on the floor, its top at 700 from x 600 to 1000.</summary>
    private static DesktopSnapshot WithWindow() =>
        new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 600, 700, 400, 340).Snapshot();

    private static bool Using(PetSimulation sim) => sim.State == PetStateId.UseFurniture;

    // ---- Choosing and getting there ----

    [Fact]
    public void A_hungry_pet_walks_to_the_trough_and_eats_from_it()
    {
        var room = new Room(Screen());
        var trough = room.Add("trough", 800);
        var sim = room.Pet(Hungry, 300);

        room.RunUntil(sim, s => s.State == PetStateId.GoToFurniture, maxTicks: 60);
        Assert.Same(trough, sim.FurnitureTarget);
        Assert.Equal(1, trough.Occupied);

        room.RunUntil(sim, Using);
        Assert.Equal(800 - 40, sim.Current.FeetPos.X, 6); // the slot on its left
        Assert.False(sim.Current.FacingLeft);             // facing the trough
        Assert.Equal(Animations.Eat.Id, sim.Current.AnimationId);

        var before = sim.Stats.Hunger;
        room.Run(sim, 600); // 10 s at 2400 points an hour
        Assert.InRange(sim.Stats.Hunger - before, 6, 7);
    }

    [Fact]
    public void A_content_pet_leaves_needs_furniture_alone()
    {
        var room = new Room(Screen());
        room.Add("trough", 800);
        var sim = room.Pet(Content, 300);

        for (var i = 0; i < 600; i++)
        {
            room.Tick(sim);
            Assert.Null(sim.FurnitureTarget);
        }
    }

    [Fact]
    public void It_climbs_a_window_to_reach_furniture_on_top()
    {
        var room = new Room(WithWindow());
        room.Add("trough", 800);
        var sim = room.Pet(Hungry, 300);

        var climbed = false;
        room.RunUntil(sim, s =>
        {
            climbed |= s.State == PetStateId.Climb;
            return Using(s);
        }, maxTicks: 3000);

        Assert.True(climbed);
        Assert.Equal(PlatformBuilder.WindowKey(Win), sim.GroundKey);
        Assert.Equal(760, sim.Current.FeetPos.X, 6);
    }

    [Fact]
    public void With_no_way_there_it_doesnt_go()
    {
        var room = new Room(WithWindow(), PetConfig.Default with { EnableClimbing = false });
        var trough = room.Add("trough", 800);
        var sim = room.Pet(Hungry, 300);

        for (var i = 0; i < 600; i++)
        {
            room.Tick(sim);
            Assert.NotEqual(PetStateId.GoToFurniture, sim.State);
        }

        Assert.Equal(0, trough.Occupied);
    }

    [Fact]
    public void Feeding_it_from_the_menu_calls_off_the_trip()
    {
        var room = new Room(Screen());
        var trough = room.Add("trough", 800);
        var sim = room.Pet(Hungry, 300);
        room.RunUntil(sim, s => s.State == PetStateId.GoToFurniture, maxTicks: 60);

        sim.Apply(PetAction.Feed);

        Assert.Null(sim.FurnitureTarget);
        Assert.Equal(0, trough.Occupied);
        Assert.Equal(PetStateId.Eat, sim.State);
    }

    [Fact]
    public void Removing_the_furniture_on_the_way_calls_off_the_trip()
    {
        var room = new Room(Screen());
        var trough = room.Add("trough", 800);
        var sim = room.Pet(Hungry, 300);
        room.RunUntil(sim, s => s.State == PetStateId.GoToFurniture, maxTicks: 60);

        room.World.Remove(trough);
        room.Tick(sim);

        Assert.Null(sim.FurnitureTarget);
        Assert.NotEqual(PetStateId.GoToFurniture, sim.State);
    }

    [Fact]
    public void Picking_the_pet_up_calls_off_the_trip()
    {
        var room = new Room(Screen());
        var trough = room.Add("trough", 800);
        var sim = room.Pet(Hungry, 300);
        room.RunUntil(sim, s => s.State == PetStateId.GoToFurniture, maxTicks: 60);
        var feet = sim.Current.FeetPos;

        sim.PointerDown(feet, 0);
        sim.PointerMove(feet + new Vec2(0, -50), 0.05);

        Assert.True(sim.IsHeld);
        Assert.Equal(0, trough.Occupied);
    }

    // ---- Uses ----

    [Fact]
    public void A_sad_pet_chases_and_kicks_the_ball()
    {
        var room = new Room(Screen());
        var ball = room.Add("ball", 700);
        var sim = room.Pet(Sad, 300);

        room.RunUntil(sim, Using);
        var kicked = false;
        for (var i = 0; i < 240; i++)
        {
            room.Tick(sim);
            kicked |= sim.Current.AnimationId == Animations.Kick.Id;
        }

        Assert.True(kicked);
        Assert.True(System.Math.Abs(ball.Feet.X - 700) > 30, $"the ball is still at {ball.Feet.X}");
        Assert.True(sim.Stats.Happiness > 10);
    }

    [Fact]
    public void The_trampoline_launches_it_onto_a_high_window()
    {
        // A window hanging high above the floor; nothing to climb.
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 300, 300, 1000, 100).Snapshot();
        var room = new Room(desktop);
        room.Add("trampoline", 800, y: 900);
        var sim = room.Pet(Sad, 400);

        room.RunUntil(sim, Using);
        Assert.Equal(-DefaultFurniture.Trampoline.Uses[0].SeatDip, sim.Current.SinkY); // on the mat

        room.RunUntil(sim, s => s.GroundKey == PlatformBuilder.WindowKey(Win), maxTicks: 600);
        Assert.Equal(300, sim.Current.FeetPos.Y);
        Assert.Null(sim.FurnitureTarget);
    }

    [Fact]
    public void A_tired_pet_naps_up_on_the_couch_and_a_click_wakes_it()
    {
        var room = new Room(Screen());
        room.Add("couch", 800);
        var sim = room.Pet(new PetStats(90, 90, 10), 300);

        room.RunUntil(sim, Using);
        Assert.Equal(Animations.Sleep.Id, sim.Current.AnimationId);
        Assert.True(sim.Current.SinkY < 0); // drawn up on the seat
        var before = sim.Stats.Energy;
        room.Run(sim, 600);
        Assert.True(sim.Stats.Energy > before);

        var feet = sim.Current.FeetPos;
        sim.PointerDown(feet, 0);
        sim.PointerUp(feet, 0.05);

        Assert.Equal(PetStateId.Idle, sim.State);
        Assert.Null(sim.FurnitureTarget);
    }

    [Fact]
    public void Two_pets_take_the_two_trough_slots()
    {
        var room = new Room(Screen());
        var trough = room.Add("trough", 800);
        var a = room.Pet(Hungry, 300);
        var b = NewSim(room.Desktop, new FakeRandom(0.99), room.Config, room.Clock, SaveData.From(Hungry, room.Clock.UtcNow), room.World);
        b.Place(new Vec2(1300, Floor - 10), Vec2.Zero);

        for (var i = 0; i < 1500 && !(Using(a) && Using(b)); i++)
        {
            room.World.Tick(Dt, room.Desktop);
            room.Clock.Advance(TimeSpan.FromSeconds(Dt));
            a.Tick(Dt, room.Desktop);
            b.Tick(Dt, room.Desktop);
        }

        Assert.True(Using(a) && Using(b));
        Assert.Equal(2, trough.Occupied);
        Assert.Equal(760, a.Current.FeetPos.X, 6);
        Assert.Equal(840, b.Current.FeetPos.X, 6);
        Assert.True(b.Current.FacingLeft);
    }

    // ---- The play pen ----

    /// <summary>Drags the pet from where it stands and lets go, still, above <paramref name="x"/>.</summary>
    private static void DropAt(PetSimulation sim, double x)
    {
        var feet = sim.Current.FeetPos;
        var grip = feet + new Vec2(0, -20);
        sim.PointerDown(grip, 0);
        sim.PointerMove(new Vec2(x, 880), 0.5);
        sim.PointerMove(new Vec2(x, 880), 0.95);
        sim.PointerUp(new Vec2(x, 880), 1.0);
    }

    [Fact]
    public void Dropped_into_the_pen_it_stays_inside_and_its_needs_dont_drop()
    {
        var room = new Room(Screen());
        var pen = room.Add("playpen", 800);
        var sim = room.Pet(Content, 300, new FakeRandom(0.0));
        room.RunUntil(sim, s => s.State == PetStateId.Idle, maxTicks: 120);

        DropAt(sim, 760);
        room.RunUntil(sim, s => s.Mode == BodyMode.Grounded, maxTicks: 120);
        Assert.Same(pen, sim.Pen);

        sim.ForceState(PetStateId.Walk, facingLeft: true);
        for (var i = 0; i < 900; i++)
        {
            room.Tick(sim);
            if (sim.State == PetStateId.Idle)
            {
                sim.ForceState(PetStateId.Walk);
            }

            Assert.InRange(sim.Current.FeetPos.X, pen.Left, pen.Right);
        }

        var before = sim.Stats;
        room.Clock.Advance(TimeSpan.FromMinutes(50));
        room.Tick(sim);
        Assert.True(sim.Stats.Hunger >= before.Hunger);
        Assert.True(sim.Stats.Happiness >= before.Happiness);

        // Taken out by the user.
        var feet = sim.Current.FeetPos;
        sim.PointerDown(feet, 0);
        sim.PointerMove(feet + new Vec2(0, -60), 0.05);
        Assert.Null(sim.Pen);
    }

    [Fact]
    public void Walking_pets_turn_at_a_pen_instead_of_going_in()
    {
        var room = new Room(Screen());
        var pen = room.Add("playpen", 800); // left side at 744
        var sim = room.Pet(Content, 600);
        room.RunUntil(sim, Grounded, maxTicks: 60);
        sim.ForceState(PetStateId.Walk, facingLeft: false);

        var turned = false;
        for (var i = 0; i < 300; i++)
        {
            room.Tick(sim);
            turned |= sim.Current.FacingLeft;
            Assert.True(sim.Current.FeetPos.X + (room.Config.SizeDip * room.Config.BodyHalfWidthFraction) <= pen.Left + 1);
        }

        Assert.True(turned);
        Assert.Null(sim.Pen);
    }

    [Fact]
    public void Furniture_is_never_chosen_from_inside_the_pen()
    {
        var room = new Room(Screen());
        room.Add("playpen", 800);
        room.Add("trough", 1400);
        var sim = room.Pet(Hungry, 300);
        room.RunUntil(sim, s => s.State == PetStateId.GoToFurniture, maxTicks: 60); // off to the trough...

        DropAt(sim, 800); // ...but put in the pen instead
        room.RunUntil(sim, s => s.Pen is not null, maxTicks: 120);
        for (var i = 0; i < 600; i++)
        {
            room.Tick(sim);
            Assert.Null(sim.FurnitureTarget);
        }
    }
}

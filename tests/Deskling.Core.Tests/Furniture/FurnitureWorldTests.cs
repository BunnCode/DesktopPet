using Deskling.Core.Furniture;
using Deskling.Core.Math;
using Deskling.Core.Simulation;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.World;
using static Deskling.Core.Tests.Fakes.SimulationExtensions;

namespace Deskling.Core.Tests.Furniture;

/// <summary>Furniture as world objects: falling, standing on windows, riding them, bouncing, dragging, saving.</summary>
public class FurnitureWorldTests
{
    private const long Win = 5;

    private static readonly FurnitureSpec Box = new()
    {
        Kind = "box", Name = "Box", WidthDip = 40, HeightDip = 20, Art = "box.png", Slots = 2,
        Uses = [new() { Need = Need.Hunger, RatePerHour = 100, Animation = "eat" }],
    };

    private static DesktopSnapshot Floor() => new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Snapshot(); // floor 1040

    private static FurnitureWorld World(params FurnitureSpec[] specs) =>
        new(PetConfig.Default, specs.Length > 0 ? specs : [Box, DefaultFurniture.Ball]);

    private static void Run(FurnitureWorld world, DesktopSnapshot desktop, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(Dt, desktop);
        }
    }

    [Fact]
    public void Furniture_falls_and_stands_on_the_floor()
    {
        var desktop = Floor();
        var world = World();
        var box = world.Add("box", new Vec2(500, 100))!;

        Run(world, desktop, 120);

        Assert.True(box.IsGrounded);
        Assert.Equal(PlatformBuilder.FloorKey(desktop.Monitors[0]), box.GroundKey);
        Assert.Equal(new Vec2(500, 1040), box.Feet);
        Assert.Equal(new RectI(480, 1020, 520, 1040), box.Current.Bounds);
    }

    [Fact]
    public void It_stands_on_a_window_rides_it_and_falls_when_it_closes()
    {
        var on = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 400, 600, 400, 300).Snapshot();
        var world = World();
        var box = world.Add("box", new Vec2(600, 100))!;
        Run(world, on, 120);
        Assert.Equal(PlatformBuilder.WindowKey(Win), box.GroundKey);
        Assert.Equal(600, box.Feet.Y);

        var moved = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Window(Win, 500, 600, 400, 300).Snapshot();
        Run(world, moved, 1);
        Assert.Equal(700, box.Feet.X);

        Run(world, Floor(), 120);
        Assert.Equal(1040, box.Feet.Y);
    }

    [Fact]
    public void A_ball_bounces_and_rolls_but_a_box_doesnt()
    {
        var desktop = Floor();
        var world = World();
        var ball = world.Add("ball", new Vec2(500, 400))!;
        var box = world.Add("box", new Vec2(900, 400))!;

        var bounced = false;
        for (var i = 0; i < 120; i++)
        {
            world.Tick(Dt, desktop);
            bounced |= ball.Feet.Y > 1000 && ball.Body.Velocity.Y < 0;
        }

        Assert.True(bounced);
        Assert.True(box.IsGrounded);

        FurnitureWorld.Kick(ball, new Vec2(400, -200));
        FurnitureWorld.Kick(box, new Vec2(400, -200));
        Run(world, desktop, 300);
        Assert.True(ball.Feet.X - 500 > 3 * (box.Feet.X - 900), $"ball {ball.Feet.X}, box {box.Feet.X}"); // low friction: it rolls on
        Assert.True(ball.IsGrounded);
    }

    [Fact]
    public void A_drag_picks_it_up_and_letting_go_throws_it()
    {
        var desktop = Floor();
        var world = World();
        var box = world.Add("box", new Vec2(500, 1040))!;
        Run(world, desktop, 2);
        var version = world.Version;

        world.PointerDown(box, new Vec2(500, 1030), 0);
        world.PointerMove(new Vec2(502, 1030), 0.01);
        Assert.False(box.IsHeld); // under the drag threshold

        world.PointerMove(new Vec2(560, 900), 0.05);
        Assert.True(box.IsHeld);
        Assert.Equal(new Vec2(560, 910), box.Feet); // held where it was grabbed
        Run(world, desktop, 10);
        Assert.Equal(new Vec2(560, 910), box.Feet); // physics leaves it alone

        world.PointerMove(new Vec2(600, 900), 0.08);
        world.PointerUp(new Vec2(640, 900), 0.1);
        Assert.False(box.IsHeld);
        Assert.True(box.Body.Velocity.X > 0);
        Assert.True(world.Version > version);
    }

    [Fact]
    public void Clicking_a_ball_pokes_it_up()
    {
        var desktop = Floor();
        var world = World();
        var ball = world.Add("ball", new Vec2(500, 1040))!;
        Run(world, desktop, 2);

        world.PointerDown(ball, new Vec2(500, 1035), 0);
        world.PointerUp(new Vec2(500, 1035), 0.1);

        Assert.True(ball.Body.Velocity.Y < 0);
    }

    [Fact]
    public void Edited_definitions_apply_and_deleted_kinds_go_away()
    {
        var world = World();
        var box = world.Add("box", new Vec2(500, 100))!;
        var ball = world.Add("ball", new Vec2(700, 100))!;

        world.SetSpecs([Box with { WidthDip = 80 }]);

        Assert.Equal(80, box.Spec.WidthDip);
        Assert.True(ball.IsRemoved);
        Assert.Equal([box], world.Items);
        Assert.Null(world.Add("ball", new Vec2(1, 1)));
    }

    [Fact]
    public void Positions_survive_a_restart()
    {
        var desktop = Floor();
        var world = World();
        world.Add("box", new Vec2(500, 100));
        world.Add("ball", new Vec2(900, 100));
        Run(world, desktop, 200);

        var save = world.CreateSave();
        var restored = World();
        restored.Load(save);
        Run(restored, desktop, 30);

        Assert.Equal(["box", "ball"], restored.Items.Select(i => i.Kind));
        Assert.Equal(new Vec2(500, 1040), restored.Items[0].Feet);
        Assert.Equal(1040, restored.Items[1].Feet.Y);
        Assert.Equal(0, restored.Version); // nothing new to save
    }

    [Fact]
    public void A_saved_kind_that_no_longer_exists_is_skipped()
    {
        var world = World();
        world.Load(new RoomSave { Items = [new RoomItem("sofa", 10, 10), new RoomItem("box", 500, 500)] });
        Assert.Equal(["box"], world.Items.Select(i => i.Kind));
    }

    [Fact]
    public void Lost_furniture_comes_back_at_the_top_of_the_screen()
    {
        var desktop = Floor();
        var world = World();
        var box = world.Add("box", new Vec2(-500, 500))!; // off screen

        Run(world, desktop, 60);

        Assert.InRange(box.Feet.X, 0, 1920);
    }

    [Fact]
    public void Slots_are_reserved_one_pet_each()
    {
        var world = World(DefaultFurniture.Tv);
        var tv = world.Add("tv", new Vec2(500, 100))!;
        var pets = Enumerable.Range(0, 5).Select(_ => new object()).ToList();

        var slots = pets.Select(tv.Reserve).ToList();

        Assert.Equal([0, 1, 2, 3, -1], slots);
        Assert.Equal(1, tv.Reserve(pets[1])); // asking again keeps its slot
        tv.Release(pets[1]);
        Assert.Equal(1, tv.Reserve(pets[4]));
        Assert.Equal(4, tv.Occupied);
    }
}

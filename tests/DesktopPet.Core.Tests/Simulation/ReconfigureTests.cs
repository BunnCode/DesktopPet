using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Tests.Fakes;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Simulation;

/// <summary>Live settings changes (settings.json saved while the pet runs).</summary>
public class ReconfigureTests
{
    private static readonly Core.World.DesktopSnapshot Desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Snapshot();

    [Fact]
    public void Size_and_speed_change_without_moving_or_resetting_the_pet()
    {
        var sim = NewSim(Desktop, new FakeRandom(0.99));
        sim.RunUntil(Desktop, Grounded);
        sim.ForceState(PetStateId.Walk, facingLeft: false);
        var feet = sim.Current.FeetPos;

        sim.Reconfigure(PetConfig.Default with { SizeDip = 128, WalkSpeedDip = 120 });
        sim.RunSeconds(Desktop, 1);

        Assert.Equal(128, sim.Current.Size.X);
        Assert.Equal(feet.X + 120, sim.Current.FeetPos.X, 0);
        Assert.Equal(1040, sim.Current.FeetPos.Y);
        Assert.Equal(PetStateId.Walk, sim.State);
    }

    [Fact]
    public void Switching_off_climbing_drops_a_clinging_pet()
    {
        var sim = NewSim(Desktop, new FakeRandom(0.6));
        sim.Place(new Vec2(1800, 400), new Vec2(1200, -300));
        sim.RunUntil(Desktop, s => s.Mode == BodyMode.Clinging, maxTicks: 60);

        sim.Reconfigure(PetConfig.Default with { EnableClimbing = false, EnablePushing = false });
        sim.Tick(Dt, Desktop);

        Assert.Equal(PetStateId.Fall, sim.State);
        Assert.Empty(sim.Walls);
    }

    [Fact]
    public void Stat_rules_apply_from_the_next_update()
    {
        var clock = new FakeClock();
        var sim = NewSim(Desktop, clock: clock, save: Core.Persistence.SaveData.From(new PetStats(80, 80, 80), clock.UtcNow));
        sim.Reconfigure(PetConfig.Default with { Stats = StatRules.Default with { HungerPerHour = -20 } });

        clock.Advance(TimeSpan.FromSeconds(30));
        sim.Tick(Dt, Desktop);

        Assert.Equal(80 - (20.0 * 30 / 3600), sim.Stats.Hunger, 6);
    }

    [Fact]
    public void Menu_push_switch_survives_a_reload_unless_the_file_changes_it()
    {
        var sim = NewSim(Desktop);
        sim.PushingEnabled = false; // switched off from the menu

        sim.Reconfigure(PetConfig.Default with { WalkSpeedDip = 70 }); // EnablePushing unchanged (true)
        Assert.False(sim.PushingEnabled);

        sim.Reconfigure(PetConfig.Default with { EnablePushing = false });
        sim.Reconfigure(PetConfig.Default with { EnablePushing = true }); // the file turned it on
        Assert.True(sim.PushingEnabled);
    }
}

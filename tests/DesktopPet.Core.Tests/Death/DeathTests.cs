using DesktopPet.Core.Death;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.World;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Death;

/// <summary>Death in the running simulation, saving a dead pet, reviving it, gravestones in the world, startup.</summary>
public class DeathTests
{
    private static readonly DesktopSnapshot Desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Snapshot();

    private static (PetSimulation Sim, FakeClock Clock) Pet(PetStats stats, PetConfig? config = null, SaveData? save = null)
    {
        var clock = new FakeClock();
        save ??= SaveData.From(stats, clock.UtcNow);
        var sim = NewSim(Desktop, clock: clock, save: save, config: config);
        sim.RunUntil(Desktop, Grounded);
        return (sim, clock);
    }

    /// <summary>Lets <paramref name="span"/> of wall-clock time pass in 30 s steps, ticking once per step.</summary>
    private static void LiveFor(PetSimulation sim, FakeClock clock, TimeSpan span)
    {
        for (var t = TimeSpan.Zero; t < span && sim.Death is null; t += TimeSpan.FromSeconds(30))
        {
            clock.Advance(TimeSpan.FromSeconds(30));
            sim.Tick(Dt, Desktop);
        }
    }

    [Fact]
    public void A_starving_pet_dies_after_the_set_time_and_its_save_says_so()
    {
        var (sim, clock) = Pet(new PetStats(0, 80, 80));
        LiveFor(sim, clock, TimeSpan.FromHours(1.9));
        Assert.Null(sim.Death);

        LiveFor(sim, clock, TimeSpan.FromHours(0.2));
        Assert.Equal(DeathCause.Hunger, sim.Death?.Cause);
        var save = sim.CreateSave();
        Assert.True(save.IsDead);
        Assert.Equal(DeathCause.Hunger, save.DiedOf);
        Assert.Equal(sim.Death!.AtUtc, save.DiedUtc);
    }

    [Fact]
    public void A_dead_pets_needs_stop_changing()
    {
        var (sim, clock) = Pet(new PetStats(0, 80, 80));
        LiveFor(sim, clock, TimeSpan.FromHours(3));
        var stats = sim.Stats;
        clock.Advance(TimeSpan.FromHours(1));
        sim.Tick(Dt, Desktop);
        Assert.Equal(stats, sim.Stats);
    }

    [Fact]
    public void Locked_screen_never_kills()
    {
        var (sim, clock) = Pet(new PetStats(0, 0, 0));
        sim.UserAway = true;
        LiveFor(sim, clock, TimeSpan.FromHours(48));
        Assert.Null(sim.Death);
    }

    [Fact]
    public void Time_with_the_app_closed_never_counts()
    {
        var clock = new FakeClock();
        var save = SaveData.From(new PetStats(0, 80, 80), clock.UtcNow - TimeSpan.FromDays(3)) with
        {
            Neglect = new NeglectTimers { HungerAtZero = 3600 },
        };
        var sim = NewSim(Desktop, clock: clock, save: save);
        sim.Tick(Dt, Desktop);
        Assert.Null(sim.Death);
        Assert.Equal(3600, sim.Neglect.HungerAtZero, 1);
    }

    [Fact]
    public void A_frozen_process_gap_doesnt_count()
    {
        var (sim, clock) = Pet(new PetStats(0, 80, 80));
        clock.Advance(TimeSpan.FromHours(10)); // the PC slept
        sim.Tick(Dt, Desktop);
        Assert.Null(sim.Death);
        Assert.Equal(0, sim.Neglect.HungerAtZero);
    }

    [Fact]
    public void Switched_off_pets_dont_die()
    {
        var config = PetConfig.Default with { Death = new DeathRules { Enabled = false } };
        var (sim, clock) = Pet(new PetStats(0, 0, 0), config);
        LiveFor(sim, clock, TimeSpan.FromHours(10));
        Assert.Null(sim.Death);
        Assert.Empty(sim.TakeWarnings());
    }

    [Fact]
    public void Feeding_in_time_saves_it()
    {
        var (sim, clock) = Pet(new PetStats(0, 80, 80));
        LiveFor(sim, clock, TimeSpan.FromHours(1.5));
        sim.Apply(PetAction.Feed);
        LiveFor(sim, clock, TimeSpan.FromHours(1));
        Assert.Null(sim.Death);
        Assert.Equal(0, sim.Neglect.HungerAtZero);
    }

    [Fact]
    public void The_countdown_survives_a_restart()
    {
        var (sim, clock) = Pet(new PetStats(0, 80, 80));
        LiveFor(sim, clock, TimeSpan.FromHours(1.5));
        var save = sim.CreateSave();
        Assert.InRange(save.Neglect!.HungerAtZero, (1.5 * 3600) - 60, (1.5 * 3600) + 60);

        var again = NewSim(Desktop, clock: clock, save: save);
        again.RunUntil(Desktop, Grounded);
        LiveFor(again, clock, TimeSpan.FromHours(0.6));
        Assert.Equal(DeathCause.Hunger, again.Death?.Cause);
    }

    [Fact]
    public void Warnings_come_out_once()
    {
        var (sim, clock) = Pet(new PetStats(16, 80, 80));
        LiveFor(sim, clock, TimeSpan.FromMinutes(30));
        var warning = Assert.Single(sim.TakeWarnings());
        Assert.Equal(new PetWarning(WarningKind.Critical, Need.Hunger), warning);
        Assert.Empty(sim.TakeWarnings());
    }

    [Fact]
    public void A_new_pet_is_born_now_and_an_old_save_keeps_its_birthday()
    {
        var clock = new FakeClock();
        Assert.Equal(clock.UtcNow, NewSim(Desktop, clock: clock).CreateSave().BornUtc);

        var born = clock.UtcNow - TimeSpan.FromDays(5);
        var save = SaveData.From(PetStats.Fresh, clock.UtcNow) with { BornUtc = born };
        Assert.Equal(born, NewSim(Desktop, clock: clock, save: save).CreateSave().BornUtc);
    }

    [Fact]
    public void A_revived_save_is_alive_with_safe_needs_and_keeps_its_birthday()
    {
        var born = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var dead = SaveData.From(new PetStats(0, 30, 40), born.AddDays(3), lastMated: born.AddDays(1)) with
        {
            BornUtc = born,
            DiedUtc = born.AddDays(3),
            DiedOf = DeathCause.Hunger,
            Neglect = new NeglectTimers { HungerAtZero = 7200 },
        };
        var now = born.AddDays(4);
        var revived = dead.Revived(new DeathRules { ReviveStat = 60 }, now);
        Assert.False(revived.IsDead);
        Assert.Null(revived.DiedOf);
        Assert.Null(revived.Neglect);
        Assert.Equal(new PetStats(60, 60, 60), revived.Stats);
        Assert.Equal(born, revived.BornUtc);
        Assert.Equal(born.AddDays(1), revived.LastMatedUtc);
        Assert.Equal(now, revived.SavedAtUtc);
    }

    [Fact]
    public void A_dead_pets_save_round_trips_through_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"desktoppet-death-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonFileSaveStore(path);
            var died = new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
            var save = SaveData.From(new PetStats(0, 10, 10), died) with
            {
                BornUtc = died.AddDays(-2),
                DiedUtc = died,
                DiedOf = DeathCause.Exhaustion,
                Neglect = new NeglectTimers { EnergyAtZero = 7200 },
            };
            store.Save(save);
            Assert.Contains("\"Exhaustion\"", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.DoesNotContain("IsZero", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(save, store.Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void An_old_save_loads_alive_with_no_birthday()
    {
        var path = Path.Combine(Path.GetTempPath(), $"desktoppet-death-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "Version": 2, "Hunger": 50, "Happiness": 50, "Energy": 50, "SavedAtUtc": "2026-11-01T00:00:00+00:00" }""");
            var save = new JsonFileSaveStore(path).Load()!;
            Assert.False(save.IsDead);
            Assert.Null(save.BornUtc);
            Assert.Null(save.Neglect);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Grave_info_comes_from_a_dead_save_only()
    {
        var died = new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
        var alive = SaveData.From(PetStats.Fresh, died);
        Assert.Null(GraveInfo.From(3, "Mia", alive));

        var grave = GraveInfo.From(3, "Mia", alive with { DiedUtc = died, DiedOf = DeathCause.Sadness, BornUtc = died.AddDays(-1) })!;
        Assert.Equal(new GraveInfo { PetId = 3, Name = "Mia", BornUtc = died.AddDays(-1), DiedUtc = died, Cause = DeathCause.Sadness }, grave);
        Assert.Equal("Mia was unhappy for too long.", grave.Story);
    }

    [Fact]
    public void Gravestones_stand_in_the_world_are_not_furniture_and_survive_a_save()
    {
        var world = new FurnitureWorld(PetConfig.Default, [DefaultFurniture.Ball]);
        var grave = new GraveInfo { PetId = 2, Name = "Leo", DiedUtc = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero) };
        var item = world.AddGrave(new Vec2(500, 100), grave);
        for (var i = 0; i < 300; i++)
        {
            world.Tick(Dt, Desktop);
        }

        Assert.True(item.IsGrounded);
        Assert.Equal(1040, item.Feet.Y, 0);
        Assert.False(item.IsFurniture);
        Assert.DoesNotContain(world.Specs, s => s.Kind == Graves.Kind);

        world.SetSpecs([]); // every furniture file deleted: the gravestone stays
        Assert.Contains(item, world.Items);

        var loaded = new FurnitureWorld(PetConfig.Default);
        loaded.Load(world.CreateSave());
        Assert.Equal(grave, Assert.Single(loaded.GraveItems).Grave);
    }

    [Fact]
    public void Updating_a_graves_details_marks_the_room_for_saving()
    {
        var world = new FurnitureWorld(PetConfig.Default);
        var grave = new GraveInfo { PetId = 2, Name = "Leo" };
        var item = world.AddGrave(new Vec2(500, 100), grave);
        var version = world.Version;
        world.SetGrave(item, grave);
        Assert.Equal(version, world.Version);
        world.SetGrave(item, grave with { Name = "Leon" });
        Assert.Equal("Leon", item.Grave!.Name);
        Assert.True(world.Version > version);
    }

    [Fact]
    public void Dead_pets_arent_started_and_all_dead_means_none()
    {
        Assert.Equal([1, 3], PetRoster.StartupPets([1, 2, 3], dead: [2]));
        Assert.Empty(PetRoster.StartupPets([1, 2], dead: [1, 2]));
        Assert.Equal([1], PetRoster.StartupPets([], dead: []));
    }
}

using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Tests.Fakes;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Persistence;

public class PetRosterTests
{
    [Theory]
    [InlineData(1, "save.json")] // unchanged from the single-pet days, so existing saves carry over
    [InlineData(2, "save-2.json")]
    [InlineData(12, "save-12.json")]
    public void Save_file_names(int id, string expected) => Assert.Equal(expected, PetRoster.SaveFileName(id));

    [Fact]
    public void Ids_come_from_save_files_only_sorted()
    {
        string[] files = ["settings.json", "save-3.json", "problems.txt", "save.json", "save-x.json", "save-1.json", "save--2.json", "SAVE-10.JSON", "save.json.tmp"];
        Assert.Equal([1, 3, 10], PetRoster.IdsFromFileNames(files));
    }

    [Fact]
    public void Startup_creates_pet_1_when_there_are_no_saves()
    {
        Assert.Equal([1], PetRoster.StartupPets(["settings.json"]));
    }

    [Fact]
    public void Startup_keeps_every_saved_pet_even_without_pet_1()
    {
        Assert.Equal([2, 5], PetRoster.StartupPets(["save-5.json", "save-2.json"])); // pet 1 was removed earlier
    }

    [Fact]
    public void Startup_is_capped()
    {
        var files = Enumerable.Range(1, 20).Select(PetRoster.SaveFileName);
        Assert.Equal(PetRoster.MaxPets, PetRoster.StartupPets(files).Count);
    }

    [Fact]
    public void Next_id_is_one_past_the_highest()
    {
        Assert.Equal(1, PetRoster.NextId([]));
        Assert.Equal(6, PetRoster.NextId([2, 5]));
    }

    [Fact]
    public void Drop_from_starts_a_fall_from_there()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080).Snapshot();
        var sim = NewSim(desktop);
        sim.RunUntil(desktop, Grounded);

        sim.DropFrom(new Vec2(300, 64));

        Assert.Equal(BodyMode.Airborne, sim.Mode);
        Assert.Equal(new Vec2(300, 64), sim.Current.FeetPos);
        sim.RunUntil(desktop, Grounded);
        Assert.Equal(new Vec2(300, 1080), sim.Current.FeetPos);
    }
}

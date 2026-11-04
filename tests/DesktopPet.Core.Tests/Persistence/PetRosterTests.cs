using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Tests.Fakes;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Persistence;

public class PetRosterTests
{
    [Theory]
    [InlineData(1, "save.json")] // the pre-M10 names, still recognised for migration
    [InlineData(2, "save-2.json")]
    [InlineData(12, "save-12.json")]
    public void Legacy_save_file_names(int id, string expected) => Assert.Equal(expected, PetRoster.LegacySaveFileName(id));

    [Fact]
    public void Legacy_ids_come_from_save_files_only_sorted()
    {
        string[] files = ["settings.json", "save-3.json", "problems.txt", "save.json", "save-x.json", "save-1.json", "save--2.json", "SAVE-10.JSON", "save.json.tmp"];
        Assert.Equal([1, 3, 10], PetRoster.LegacyIdsFromFileNames(files));
    }

    [Fact]
    public void Ids_come_from_numbered_folders_only_sorted()
    {
        string[] folders = ["3", "sprites", "1", "01", "0", "-2", "x", "12"];
        Assert.Equal([1, 3, 12], PetRoster.IdsFromFolderNames(folders));
    }

    [Fact]
    public void Startup_creates_pet_1_when_there_are_no_pets()
    {
        Assert.Equal([1], PetRoster.StartupPets([]));
    }

    [Fact]
    public void Startup_keeps_every_pet_even_without_pet_1()
    {
        Assert.Equal([2, 5], PetRoster.StartupPets([5, 2])); // pet 1 was removed earlier
    }

    [Fact]
    public void Startup_is_capped()
    {
        Assert.Equal(PetRoster.MaxPets, PetRoster.StartupPets(Enumerable.Range(1, 20)).Count);
    }

    [Fact]
    public void Migration_moves_old_saves_into_matching_folders()
    {
        var moves = PetRoster.PlanMigration(["settings.json", "save.json", "save-3.json"], []);
        Assert.Equal([("save.json", 1), ("save-3.json", 3)], moves);
    }

    [Fact]
    public void Migration_never_overwrites_an_existing_pet_folder()
    {
        // Folder 1 exists (say, copied in); save.json goes to a new id past every folder and every old save.
        var moves = PetRoster.PlanMigration(["save.json", "save-4.json"], [1, 2]);
        Assert.Equal([("save.json", 5), ("save-4.json", 4)], moves);
    }

    [Fact]
    public void Migration_with_nothing_old_does_nothing()
    {
        Assert.Empty(PetRoster.PlanMigration(["settings.json", "problems.txt"], [1]));
    }

    [Theory]
    [InlineData(@"3\save.json", 3, PetFileKind.Save)]
    [InlineData("3/SAVE.JSON", 3, PetFileKind.Save)]
    [InlineData(@"2\personality.txt", 2, PetFileKind.Personality)]
    [InlineData(@"2\sprites", 2, PetFileKind.Sprites)]
    [InlineData(@"2\sprites\atlas.json", 2, PetFileKind.Sprites)]
    [InlineData(@"2\sprites\walk\a.png", 2, PetFileKind.Sprites)]
    public void Classifies_pet_file_changes(string path, int id, PetFileKind kind)
    {
        Assert.Equal((id, kind), PetRoster.Classify(path));
    }

    [Theory]
    [InlineData(@"3\save.json.tmp")]
    [InlineData(@"3\save.bad.json")]
    [InlineData(@"3\traits.json")] // folded into save.json at startup
    [InlineData("3")]
    [InlineData(@"x\save.json")]
    [InlineData(@"3\notes\save.json")]
    [InlineData("save.json")]
    public void Ignores_other_changes(string path)
    {
        Assert.Null(PetRoster.Classify(path));
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

using DesktopPet.Core.Persistence;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Tests.Persistence;

public sealed class JsonFileSaveStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesktopPetTests", Guid.NewGuid().ToString("N"));

    private string SavePath => Path.Combine(_dir, "nested", "save.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_loads_as_no_save()
    {
        Assert.Null(new JsonFileSaveStore(SavePath).Load());
    }

    [Fact]
    public void Round_trips_and_creates_the_folder()
    {
        var store = new JsonFileSaveStore(SavePath);
        var data = SaveData.From(new PetStats(12.5, 67, 89), new DateTimeOffset(2026, 11, 3, 9, 30, 0, TimeSpan.Zero));

        store.Save(data);
        var loaded = store.Load();

        Assert.Equal(data, loaded);
        Assert.False(File.Exists(SavePath + ".tmp")); // temp file swapped in, not left behind
    }

    [Fact]
    public void Overwrites_previous_save()
    {
        var store = new JsonFileSaveStore(SavePath);
        store.Save(SaveData.From(new PetStats(1, 2, 3), DateTimeOffset.UnixEpoch));
        store.Save(SaveData.From(new PetStats(4, 5, 6), DateTimeOffset.UnixEpoch));
        Assert.Equal(new PetStats(4, 5, 6), store.Load()!.Stats);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("{\"Version\": 0}")]
    [InlineData("null")]
    public void Corrupt_or_unknown_file_loads_as_no_save(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, contents);
        Assert.Null(new JsonFileSaveStore(SavePath).Load());
    }

    [Fact]
    public void Out_of_range_values_are_clamped_on_load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, "{\"Version\":1,\"Hunger\":250,\"Happiness\":-40,\"Energy\":50,\"SavedAtUtc\":\"2026-11-03T00:00:00+00:00\"}");
        Assert.Equal(new PetStats(100, 0, 50), new JsonFileSaveStore(SavePath).Load()!.Stats);
    }
}

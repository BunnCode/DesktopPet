using Deskling.Core.Persistence;
using Deskling.Core.Stats;
using Deskling.Core.Traits;

namespace Deskling.Core.Tests.Persistence;

/// <summary>save.json with its Traits section: reading, keeping hand edits on save, and folding in an old traits.json.</summary>
public sealed class PetSaveFileTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly PetTraits Mia = new()
    {
        Name = "Mia",
        Neediness = new Neediness(1.5, 0.8, 1.2),
        Behavior = new BehaviorTraits(Climb: 1.9),
        Jacket = "#3E455A",
    };

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesklingTests", Guid.NewGuid().ToString("N"));

    private string SavePath => Path.Combine(_dir, "save.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static SaveData Save(PetStats stats, PetTraits? traits = null) => SaveData.From(stats, Now) with { Traits = traits };

    [Fact]
    public void Round_trips_stats_and_traits_with_the_traits_near_the_top()
    {
        var data = Save(new PetStats(10, 20, 30), Mia) with { BornUtc = Now.AddDays(-3) };
        var text = PetSaveFile.Serialize(data);
        var parsed = PetSaveFile.Parse(text);

        Assert.Equal(data, parsed.Save);
        Assert.Empty(parsed.Traits!.Errors);
        Assert.StartsWith("// This pet's save.", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("\"Traits\"", StringComparison.Ordinal) < text.IndexOf("\"Hunger\"", StringComparison.Ordinal));
        Assert.Equal(4, parsed.Save!.Version);
        Assert.Contains("+00:00", text, StringComparison.Ordinal); // readable, not +
        Assert.DoesNotContain("\"Stats\"", text, StringComparison.Ordinal); // computed from the three stats
    }

    [Fact]
    public void Names_outside_ascii_stay_readable()
    {
        var text = PetSaveFile.Serialize(Save(PetStats.Fresh, Mia with { Name = "Zoë" }));

        Assert.Contains("\"Zoë\"", text, StringComparison.Ordinal);
        Assert.Equal("Zoë", PetSaveFile.Parse(text).Save!.Traits!.Name);
    }

    [Fact]
    public void A_save_without_traits_has_no_traits_section()
    {
        var parsed = PetSaveFile.Parse(PetSaveFile.Serialize(Save(PetStats.Fresh)));

        Assert.NotNull(parsed.Save);
        Assert.Null(parsed.Traits);
        Assert.Null(parsed.Save.Traits);
    }

    [Fact]
    public void Is_forgiving_about_form()
    {
        const string text = """
            // hand-edited
            { "version": 4, "traits": { "name": " Wilbur ", "behavior": { "climb": 3, }, },
              "hunger": 40, "happiness": 50, "energy": 60, "savedAtUtc": "2026-11-04T12:00:00+00:00", }
            """;
        var parsed = PetSaveFile.Parse(text);

        Assert.Equal(new PetStats(40, 50, 60), parsed.Save!.Stats);
        Assert.Equal("Wilbur", parsed.Save.Traits!.Name);
        Assert.Equal(new BehaviorTraits(Climb: 3), parsed.Save.Traits.Behavior);
    }

    [Theory]
    [InlineData("""{ "Name": "Ava", "Neediness": { "Hunger": 11 } }""", "Neediness.Hunger must be between 0 and 10 (it is 11).")]
    [InlineData("""{ "Name": "Ava", "Neediness": { "Hunger": "lots" } }""", "Traits has a value of the wrong kind at Traits.Neediness.Hunger.")]
    [InlineData("null", "Traits is empty.")]
    public void Broken_traits_are_reported_but_the_stats_still_load(string traits, string error)
    {
        var text = $$"""{ "Version": 4, "Traits": {{traits}}, "Hunger": 40, "Happiness": 50, "Energy": 60, "SavedAtUtc": "2026-11-04T12:00:00+00:00" }""";
        var parsed = PetSaveFile.Parse(text);

        Assert.Equal(new PetStats(40, 50, 60), parsed.Save!.Stats);
        Assert.Null(parsed.Save.Traits);
        Assert.Null(parsed.Traits!.Traits);
        Assert.Contains(error, parsed.Traits.Errors);
    }

    [Theory]
    [InlineData("{ \"Version\": 4, \"Traits\": { \"Name\": \"Ava\" ")]
    [InlineData("")]
    [InlineData("null")]
    public void A_file_that_isnt_json_has_no_save_and_says_why(string text)
    {
        var parsed = PetSaveFile.Parse(text);

        Assert.Null(parsed.Save);
        Assert.Null(parsed.Traits!.Traits);
        Assert.Single(parsed.Traits.Errors);
    }

    // ---- Saving over the file ----

    [Fact]
    public void Saving_keeps_the_traits_in_the_file_exactly_as_they_are()
    {
        // The user is mid-edit: the name is fine, the hunger isn't valid yet. The autosave mustn't undo that edit.
        var onDisk = """{ "Version": 4, "Traits": { "Name": "Mia", "Neediness": { "Hunger": 12 } }, "Hunger": 1, "Happiness": 1, "Energy": 1 }""";
        var (text, broken) = PetSaveFile.Merge(onDisk, Save(new PetStats(70, 80, 90), Mia), replaceTraits: false);
        var parsed = PetSaveFile.Parse(text);

        Assert.False(broken);
        Assert.Equal(new PetStats(70, 80, 90), parsed.Save!.Stats); // stats are the app's
        Assert.Contains("Neediness.Hunger must be between 0 and 10 (it is 12).", parsed.Traits!.Errors);
    }

    [Fact]
    public void Saving_writes_the_pets_traits_when_the_file_has_none_or_they_are_replaced()
    {
        var withoutTraits = PetSaveFile.Serialize(Save(PetStats.Fresh));
        var withOtherTraits = PetSaveFile.Serialize(Save(PetStats.Fresh, PetTraits.Neutral("Old")));

        Assert.Equal(Mia, PetSaveFile.Parse(PetSaveFile.Merge(null, Save(PetStats.Fresh, Mia), false).Text).Save!.Traits);
        Assert.Equal(Mia, PetSaveFile.Parse(PetSaveFile.Merge(withoutTraits, Save(PetStats.Fresh, Mia), false).Text).Save!.Traits);
        Assert.Equal(Mia, PetSaveFile.Parse(PetSaveFile.Merge(withOtherTraits, Save(PetStats.Fresh, Mia), true).Text).Save!.Traits);
        Assert.Equal("Old", PetSaveFile.Parse(PetSaveFile.Merge(withOtherTraits, Save(PetStats.Fresh, Mia), false).Text).Save!.Traits!.Name);
    }

    [Fact]
    public void Saving_over_a_file_that_isnt_json_says_to_keep_a_copy()
    {
        var (text, broken) = PetSaveFile.Merge("{ \"Traits\": { oops", Save(PetStats.Fresh, Mia), replaceTraits: false);

        Assert.True(broken);
        Assert.Equal(Mia, PetSaveFile.Parse(text).Save!.Traits);
    }

    [Fact]
    public void The_store_keeps_a_broken_file_as_save_bad_json()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SavePath, "{ broken by hand");
        var store = new JsonFileSaveStore(SavePath);
        string? kept = null;
        store.BrokenFileKept += (_, path) => kept = path;

        store.Save(Save(PetStats.Fresh, Mia));

        Assert.Equal(Path.Combine(_dir, "save.bad.json"), kept);
        Assert.Equal("{ broken by hand", File.ReadAllText(kept!));
        Assert.Equal(Mia, store.Load()!.Traits);
    }

    [Fact]
    public void The_store_keeps_a_hand_edit_of_the_traits_through_an_autosave()
    {
        var store = new JsonFileSaveStore(SavePath);
        store.Save(Save(PetStats.Fresh, Mia));
        File.WriteAllText(SavePath, File.ReadAllText(SavePath).Replace("\"Mia\"", "\"Mimi\"", StringComparison.Ordinal));

        store.Save(Save(new PetStats(1, 2, 3), Mia));

        var loaded = store.Load()!;
        Assert.Equal("Mimi", loaded.Traits!.Name);
        Assert.Equal(new PetStats(1, 2, 3), loaded.Stats);
    }

    // ---- Folding in traits.json (saves before version 4) ----

    [Fact]
    public void An_old_traits_file_folds_into_the_save()
    {
        const string oldSave = """{ "Version": 3, "Hunger": 40, "Happiness": 50, "Energy": 60, "SavedAtUtc": "2026-11-03T12:00:00+00:00", "BornUtc": "2026-11-01T00:00:00+00:00" }""";
        var oldTraits = "// This pet's traits.\n" + PetTraitsFile.Serialize(Mia);

        var (text, error) = PetSaveFile.FoldIn(oldSave, oldTraits, Now);
        var save = PetSaveFile.Parse(text!).Save!;

        Assert.Null(error);
        Assert.Equal(SaveData.CurrentVersion, save.Version);
        Assert.Equal(Mia, save.Traits);
        Assert.Equal(new PetStats(40, 50, 60), save.Stats);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), save.BornUtc);
    }

    [Fact]
    public void Traits_without_a_save_fold_into_a_fresh_one()
    {
        var (text, _) = PetSaveFile.FoldIn(null, PetTraitsFile.Serialize(Mia), Now);
        var save = PetSaveFile.Parse(text!).Save!;

        Assert.Equal(PetStats.Fresh, save.Stats);
        Assert.Equal(Now, save.SavedAtUtc);
        Assert.Equal(Mia, save.Traits);
    }

    [Fact]
    public void Invalid_traits_values_fold_in_as_written()
    {
        var (text, error) = PetSaveFile.FoldIn(null, """{ "Name": "Ava", "Behavior": { "Push": -1 } }""", Now);

        Assert.Null(error);
        Assert.Contains("Behavior.Push must be between 0 and 10 (it is -1).", PetSaveFile.Parse(text!).Traits!.Errors);
    }

    [Theory]
    [InlineData(null, "{ nope", "traits.json: Not valid JSON (line 1).")]
    [InlineData("{ nope", """{ "Name": "Ava" }""", "save.json: Not valid JSON (line 1).")]
    public void Files_that_arent_json_dont_fold_in(string? save, string traits, string error)
    {
        Assert.Equal((null, error), PetSaveFile.FoldIn(save, traits, Now));
    }
}

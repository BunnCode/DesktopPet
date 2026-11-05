using Deskling.Core.Persistence;
using Deskling.Core.Stats;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.Traits;

namespace Deskling.Core.Tests.Traits;

/// <summary>The personality trait list (personality-traits.json), drawing from it, its nudges; the new traits.json fields.</summary>
public class PersonalityTraitsTests
{
    private static PersonalityTrait Trait(string text, params (string Key, double Factor)[] nudge) =>
        new(text, nudge.ToDictionary(n => n.Key, n => n.Factor, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void The_defaults_round_trip_through_the_file()
    {
        var parsed = PersonalityTraits.Parse(PersonalityTraits.Serialize(PersonalityTraits.Defaults));
        Assert.Empty(parsed.Errors);
        Assert.Equal(PersonalityTraits.Defaults.Count, parsed.Traits!.Count);
        Assert.Equal("is voracious", parsed.Traits[0].Text);
        Assert.Equal(1.6, parsed.Traits[0].Nudge["Hunger"]);
        Assert.Contains(parsed.Traits, t => t.Text == "is extremely interested in mating" && t.Nudge["Mating"] > 1);
    }

    [Fact]
    public void A_hand_edited_file_is_forgiving_about_casing_and_strict_about_values()
    {
        var ok = PersonalityTraits.Parse("""
            // mine
            [ { "text": "loves carrots", "nudge": { "hunger": 1.3 } }, { "Text": "is calm" }, ]
            """);
        Assert.Empty(ok.Errors);
        Assert.Equal(1.3, ok.Traits![0].Nudge["Hunger"]);
        Assert.Empty(ok.Traits[1].Nudge);

        var bad = PersonalityTraits.Parse("""[ { "Text": "" }, { "Text": "x", "Nudge": { "Speed": 2, "Walk": 20 } } ]""");
        Assert.Null(bad.Traits);
        Assert.Equal(3, bad.Errors.Count);
        Assert.Contains(bad.Errors, e => e.Contains("Speed", StringComparison.Ordinal));
        Assert.Contains(bad.Errors, e => e.Contains("Walk must be between 0 and 10", StringComparison.Ordinal));

        Assert.NotEmpty(PersonalityTraits.Parse("{ not json").Errors);
    }

    [Fact]
    public void Traits_pulling_a_number_opposite_ways_dont_go_together()
    {
        var lazy = Trait("is lazy", ("Sit", 1.6), ("Walk", 0.7));
        var restless = Trait("can't sit still", ("Walk", 1.6));
        var hungry = Trait("is voracious", ("Hunger", 1.6));
        Assert.False(PersonalityTraits.Compatible(lazy, restless));
        Assert.True(PersonalityTraits.Compatible(lazy, hungry));
        Assert.False(PersonalityTraits.Compatible(hungry, hungry));

        // Always the first remaining one: lazy, then restless is skipped, then hungry; count 2 (0.0 → MinCount).
        var drawn = PersonalityTraits.Draw([lazy, restless, hungry], new FakeRandom(0.0));
        Assert.Equal(["is lazy", "is voracious"], drawn.Select(d => d.Text));
        Assert.Equal(3, PersonalityTraits.Draw(PersonalityTraits.Defaults, new FakeRandom(0.99)).Count);
    }

    [Fact]
    public void Drawn_traits_nudge_the_numbers_and_make_the_sentence()
    {
        var traits = new PetTraits
        {
            Name = "Mia", Neediness = new Neediness(Hunger: 1.5), Behavior = new BehaviorTraits(Walk: 8), Character = "emberdonkey",
        };
        var list = new[] { Trait("is voracious", ("Hunger", 1.6)), Trait("can't sit still", ("Walk", 1.6)) };

        var (given, personality) = PersonalityTraits.Give(traits, list, new FakeRandom(0.0));
        Assert.Equal(2.4, given.Neediness.Hunger);
        Assert.Equal(10, given.Behavior.Walk); // capped
        Assert.Equal(1, given.Behavior.Sit);
        Assert.Equal("Mia is a little fire donkey who is voracious and can't sit still.", personality);

        var (plain, described) = PersonalityTraits.Give(traits, [], new FakeRandom(0.0));
        Assert.Equal(traits, plain);
        Assert.Equal(Personality.Describe(traits), described);
        Assert.Equal("Leo is a little tan foal who is a, b and c.", PersonalityTraits.Sentence("Leo", "little tan foal", ["is a", "b", "c"]));
    }

    [Fact]
    public void Traits_json_keeps_fixed_jacket_parents_and_mating()
    {
        var traits = new PetTraits
        {
            Name = "Kit", Fixed = true, Jacket = "#693044", Parents = ["Mia", "Leo"], Behavior = new BehaviorTraits(Mating: 2.5),
        };
        var parsed = PetTraitsFile.Parse(PetTraitsFile.Serialize(traits));
        Assert.Empty(parsed.Errors);
        Assert.Equal(traits, parsed.Traits);

        var plain = PetTraitsFile.Serialize(PetTraits.Neutral("Ann"));
        Assert.DoesNotContain("\"Jacket\"", plain, StringComparison.Ordinal); // left out until it has one
        Assert.False(PetTraitsFile.Parse(plain).Traits!.Fixed);

        var bad = PetTraitsFile.Parse("""{ "Name": "Ann", "Jacket": "blue", "Behavior": { "Mating": 11 } }""");
        Assert.Equal(2, bad.Errors.Count);
    }

    [Fact]
    public void Old_saves_load_without_a_mating_time()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deskling-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "Version": 1, "Hunger": 50, "Happiness": 60, "Energy": 70, "SavedAtUtc": "2026-11-03T12:00:00+00:00" }""");
            var store = new JsonFileSaveStore(path);
            var old = store.Load();
            Assert.NotNull(old);
            Assert.Null(old.LastMatedUtc);

            var mated = SaveData.From(PetStats.Fresh, DateTimeOffset.UnixEpoch, lastMated: DateTimeOffset.UnixEpoch);
            store.Save(mated);
            Assert.Equal(mated, store.Load());
        }
        finally
        {
            File.Delete(path);
        }
    }
}

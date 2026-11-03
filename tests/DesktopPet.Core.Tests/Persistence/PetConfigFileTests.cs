using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Tests.Persistence;

public class PetConfigFileTests
{
    [Fact]
    public void Defaults_round_trip_exactly()
    {
        var text = PetConfigFile.Serialize(PetConfig.Default);
        var result = PetConfigFile.Parse(text);

        Assert.Empty(result.Errors);
        Assert.Equal(PetConfig.Default, result.Config);
    }

    [Fact]
    public void Written_file_starts_with_a_comment_header_and_is_still_readable()
    {
        var text = PetConfigFile.Serialize(PetConfig.Default);
        Assert.StartsWith("// DesktopPet settings.", text, StringComparison.Ordinal);
        Assert.Contains("\"WalkSpeedDip\"", text, StringComparison.Ordinal);
        Assert.Contains("\"Stats\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_keys_keep_their_defaults()
    {
        var result = PetConfigFile.Parse("""{ "WalkSpeedDip": 90 }""");

        Assert.Empty(result.Errors);
        Assert.Equal(PetConfig.Default with { WalkSpeedDip = 90 }, result.Config);
    }

    [Fact]
    public void Nested_records_can_be_partially_overridden()
    {
        var result = PetConfigFile.Parse("""
            {
              "Weights": { "Walk": 9 },
              "IdleSeconds": { "Min": 1, "Max": 2 },
              "Stats": { "Critical": 10, "AwayGap": "00:05:00" }
            }
            """);

        Assert.Empty(result.Errors);
        var c = result.Config!;
        Assert.Equal(new BehaviorWeights(Walk: 9), c.Weights);
        Assert.Equal(new DurationRange(1, 2), c.IdleSeconds);
        Assert.Equal(10, c.Stats.Critical);
        Assert.Equal(TimeSpan.FromMinutes(5), c.Stats.AwayGap);
        Assert.Equal(PetConfig.Default.Stats.HungerPerHour, c.Stats.HungerPerHour);
    }

    [Fact]
    public void Hand_editing_comforts_are_allowed()
    {
        var result = PetConfigFile.Parse("""
            // my tweaks
            {
              "walkspeeddip": 75,   /* any casing */
              "EnablePushing": false,
              "SomethingUnknown": 1,
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Equal(75, result.Config!.WalkSpeedDip);
        Assert.False(result.Config.EnablePushing);
    }

    [Fact]
    public void Broken_json_reports_the_line()
    {
        var result = PetConfigFile.Parse("{\n  \"WalkSpeedDip\": 90\n  \"SizeDip\": 64\n}");
        Assert.Null(result.Config);
        var error = Assert.Single(result.Errors);
        Assert.Contains("line 3", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "SizeDip": 5 }""", "SizeDip")]
    [InlineData("""{ "WallRestitution": 1.5 }""", "WallRestitution")]
    [InlineData("""{ "ClimbChance": 0.7, "PushChance": 0.5 }""", "ClimbChance and PushChance")]
    [InlineData("""{ "WalkSeconds": { "Min": 5, "Max": 2 } }""", "WalkSeconds")]
    [InlineData("""{ "Weights": { "Idle": 0, "Walk": 0, "Sit": 0 } }""", "Weights.Idle")]
    [InlineData("""{ "Stats": { "Critical": 30, "AwayFloor": 20 } }""", "Stats.AwayFloor")]
    [InlineData("""{ "Stats": { "AwayGap": "00:00:00" } }""", "Stats.AwayGap")]
    public void Out_of_range_values_are_rejected_by_key(string json, string key)
    {
        var result = PetConfigFile.Parse(json);
        Assert.Null(result.Config);
        Assert.Contains(result.Errors, e => e.StartsWith(key, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_problem_is_listed_not_just_the_first()
    {
        var result = PetConfigFile.Parse("""{ "SizeDip": 1, "GravityDip": -5, "WalkTurnChance": 2 }""");
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Empty_file_is_an_error()
    {
        Assert.Null(PetConfigFile.Parse("null").Config);
    }

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.Empty(PetConfigValidator.Validate(PetConfig.Default));
    }
}

using DesktopPet.Core.Animation;
using DesktopPet.Core.Furniture;

namespace DesktopPet.Core.Tests.Furniture;

public class FurnitureSpecFileTests
{
    private static readonly string[] AnimationIds = [.. Animations.All.Select(a => a.Id)];

    private static FurnitureParseResult Parse(string json, string kind = "thing") => FurnitureSpecFile.Parse(kind, json, AnimationIds);

    [Fact]
    public void Every_default_survives_a_round_trip_through_its_file()
    {
        foreach (var spec in DefaultFurniture.All)
        {
            var result = Parse(FurnitureSpecFile.Serialize(spec), spec.Kind);

            Assert.Empty(result.Errors);
            Assert.Equal(spec.Kind, result.Spec!.Kind);
            Assert.Equal(FurnitureSpecFile.Serialize(spec), FurnitureSpecFile.Serialize(result.Spec));
        }
    }

    [Fact]
    public void The_defaults_cover_the_planned_furniture()
    {
        Assert.Equal(["ball", "tv", "couch", "trampoline", "bed", "playpen", "trough", "pill"], DefaultFurniture.All.Select(s => s.Kind));
        Assert.True(DefaultFurniture.PlayPen.IsPen);
        Assert.Equal(4, DefaultFurniture.Tv.Slots);
        Assert.Contains(DefaultFurniture.Ball.Uses, u => u.Effect == UseEffect.Kick);
        Assert.True(DefaultFurniture.Pill.Consumable);
        Assert.Equal(UseEffect.Breed, Assert.Single(DefaultFurniture.Pill.Uses).Effect);
    }

    [Fact]
    public void A_folder_from_before_the_pill_gets_only_the_pill_and_a_deleted_default_stays_deleted()
    {
        Assert.Equal(["pill"], DefaultFurniture.ToWrite(null).Select(s => s.Kind));
        Assert.Empty(DefaultFurniture.ToWrite([.. DefaultFurniture.All.Select(s => s.Kind)]));
        Assert.Equal(["tv", "pill"], DefaultFurniture.ToWrite(["ball", "couch", "trampoline", "bed", "playpen", "trough"]).Select(s => s.Kind));
    }

    [Fact]
    public void Consumable_and_the_breed_effect_are_read_from_a_file()
    {
        var result = Parse("""{ "name": "Pill", "art": "pill.png", "consumable": true, "uses": [ { "animation": "eat", "effect": "breed" } ] }""");

        Assert.Empty(result.Errors);
        Assert.True(result.Spec!.Consumable);
        Assert.Equal(UseEffect.Breed, result.Spec.Uses[0].Effect);
    }

    [Theory]
    [InlineData("Ball.json", "ball")]
    [InlineData("play pen.JSON", "play pen")]
    [InlineData("ball.png", null)]
    [InlineData(".json", null)]
    public void The_kind_is_the_file_name(string file, string? kind) => Assert.Equal(kind, FurnitureSpecFile.KindFromFileName(file));

    [Fact]
    public void Hand_edits_are_forgiven_comments_casing_trailing_commas_and_enum_case()
    {
        var result = Parse("""
            // my furniture
            {
              "name": "Box", "art": "box.png", "widthDip": 40,
              "uses": [ { "need": "hunger", "ratePerHour": 100, "animation": "eat", "effect": "none", }, ],
            }
            """);

        Assert.Empty(result.Errors);
        var spec = result.Spec!;
        Assert.Equal("Box", spec.Name);
        Assert.Equal(40, spec.WidthDip);
        Assert.Equal(32, spec.HeightDip); // default
        Assert.Equal(Need.Hunger, spec.Uses[0].Need);
    }

    [Fact]
    public void Bad_values_are_reported_by_key()
    {
        var result = Parse("""
            { "Name": "", "Art": "..\\evil.png", "WidthDip": 2, "Slots": 2, "SlotOffsetsDip": [1],
              "Uses": [ { "Animation": "dance", "RatePerHour": -5, "Seconds": { "Min": 5, "Max": 1 } } ] }
            """);

        Assert.Null(result.Spec);
        Assert.Contains(result.Errors, e => e.StartsWith("Name", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("Art must be a file name", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("WidthDip", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("SlotOffsetsDip needs", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Contains("'dance'", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("Uses[0].RatePerHour", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("Uses[0].Seconds", StringComparison.Ordinal));
    }

    [Fact]
    public void Broken_json_says_where()
    {
        var result = Parse("{\n \"Name\": \"x\",\n oops }");
        Assert.Null(result.Spec);
        Assert.Contains("line 3", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Slots_spread_evenly_unless_given()
    {
        var spec = new FurnitureSpec { WidthDip = 100, Slots = 4 };
        Assert.Equal([-37.5, -12.5, 12.5, 37.5], Enumerable.Range(0, 4).Select(spec.SlotOffsetDip));
        Assert.Equal(-90, DefaultFurniture.Tv.SlotOffsetDip(0));
        Assert.Equal(0, new FurnitureSpec { Slots = 1 }.SlotOffsetDip(0));
    }
}

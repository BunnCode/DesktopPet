using DesktopPet.Core.Animation;
using DesktopPet.Core.Math;
using DesktopPet.Core.Rendering;

namespace DesktopPet.Core.Tests.Rendering;

public class SpriteSheetParserTests
{
    private static readonly AnimationDef Idle = new("idle", 4, 8, true);
    private static readonly AnimationDef Walk = new("walk", 4, 8, true);
    private static readonly AnimationDef Fall = new("fall", 2, 10, true);
    private static readonly AnimationDef[] Anims = [Idle, Walk, Fall];

    /// <summary>A sprites folder with a 256×128 sheet.png and a 64×32 walk.png.</summary>
    private static (int, int)? Size(string image) => image switch
    {
        "sheet.png" => (256, 128),
        "walk.png" => (64, 32),
        _ => null,
    };

    private static SpriteSheetSpec Parse(string json) => SpriteSheetParser.Parse(json, Anims, Size);

    [Fact]
    public void Frames_run_along_a_row_from_the_given_column()
    {
        var spec = Parse("""{ "image": "sheet.png", "animations": { "idle": { "row": 1, "column": 2 } } }""");

        Assert.Empty(spec.Problems);
        Assert.Equal(
            [new RectI(64, 32, 96, 64), new RectI(96, 32, 128, 64), new RectI(128, 32, 160, 64), new RectI(160, 32, 192, 64)],
            spec.Animations["idle"].Select(f => f.Rect));
        Assert.All(spec.Animations["idle"], f => Assert.Equal("sheet.png", f.Image));
    }

    [Fact]
    public void Undefined_animations_use_the_built_in_art()
    {
        var spec = Parse("""{ "image": "sheet.png", "animations": { "idle": { "row": 0 } } }""");
        Assert.Equal(["walk", "fall"], spec.UsingDefault);
        Assert.False(spec.Animations.ContainsKey("walk"));
    }

    [Fact]
    public void Fewer_frames_cycle_and_extras_are_ignored()
    {
        var spec = Parse("""
            { "image": "sheet.png", "animations": { "idle": { "row": 0, "frames": 2 }, "fall": { "row": 1, "frames": 5 } } }
            """);

        Assert.Equal([0, 32, 0, 32], spec.Animations["idle"].Select(f => f.Rect.Left));
        Assert.Equal(2, spec.Animations["fall"].Count);
    }

    [Fact]
    public void Animation_can_use_its_own_image_and_cell_size()
    {
        var spec = Parse("""
            { "image": "sheet.png", "animations": { "walk": { "image": "walk.png", "frames": 4, "frameWidth": 16, "frameHeight": 32 } } }
            """);

        Assert.Empty(spec.Problems);
        Assert.Equal(new RectI(48, 0, 64, 32), spec.Animations["walk"][3].Rect);
        Assert.Equal("walk.png", spec.Animations["walk"][0].Image);
    }

    [Fact]
    public void Frame_outside_the_image_is_a_problem_and_falls_back()
    {
        var spec = Parse("""{ "image": "sheet.png", "animations": { "idle": { "row": 3, "column": 6 } } }""");

        var problem = Assert.Single(spec.Problems);
        Assert.Contains("\"idle\"", problem, StringComparison.Ordinal);
        Assert.Contains("256×128", problem, StringComparison.Ordinal);
        Assert.Contains("idle", spec.UsingDefault);
    }

    [Fact]
    public void Missing_image_unknown_animation_and_bad_paths_are_reported()
    {
        var spec = Parse("""
            {
              "animations": {
                "idle": { "image": "nope.png" },
                "jump": { "row": 0, "image": "sheet.png" },
                "walk": { "image": "../secret.png" },
                "fall": { }
              }
            }
            """);

        Assert.Equal(4, spec.Problems.Count);
        Assert.Contains(spec.Problems, p => p.Contains("nope.png", StringComparison.Ordinal));
        Assert.Contains(spec.Problems, p => p.Contains("Unknown animation \"jump\"", StringComparison.Ordinal));
        Assert.Contains(spec.Problems, p => p.Contains("inside the sprites folder", StringComparison.Ordinal));
        Assert.Contains(spec.Problems, p => p.Contains("no image", StringComparison.Ordinal));
        Assert.Empty(spec.Animations);
    }

    [Fact]
    public void Broken_json_means_all_built_in_art()
    {
        var spec = Parse("{ \"image\": ");
        Assert.Single(spec.Problems);
        Assert.Equal(Anims.Length, spec.UsingDefault.Count);
    }

    [Fact]
    public void Options_are_read_and_ids_are_case_insensitive()
    {
        var spec = Parse("""
            // painted art, drawn facing left
            { "image": "sheet.png", "facesLeft": true, "smooth": true, "animations": { "IDLE": { "row": 0 }, } }
            """);

        Assert.True(spec.FacesLeft);
        Assert.True(spec.Smooth);
        Assert.True(spec.Animations.ContainsKey("idle"));
    }

    [Fact]
    public void Template_atlas_parses_back_to_every_animation()
    {
        var all = Animations.All;
        var json = SpriteSheetParser.TemplateJson(all, "template.png", 32);
        var width = all.Max(a => a.FrameCount) * 32;
        var spec = SpriteSheetParser.Parse(json, all, image => image == "template.png" ? (width, all.Count * 32) : null);

        Assert.Empty(spec.Problems);
        Assert.Empty(spec.UsingDefault);
        Assert.All(all, a => Assert.Equal(a.FrameCount, spec.Animations[a.Id].Count));
    }
}

using System.Text.Json;
using Deskling.Core.Animation;
using Deskling.Core.Math;

namespace Deskling.Core.Rendering;

/// <summary>One frame of custom art: a rectangle in an image file (relative to the sprites folder).</summary>
public sealed record SpriteFrame(string Image, RectI Rect);

/// <summary>
/// The result of reading <c>sprites/atlas.json</c>: frames for the animations that were defined correctly,
/// plus everything the user should know (problems, and which animations still use the built-in art).
/// </summary>
/// <param name="Animations">Animation id → exactly <see cref="AnimationDef.FrameCount"/> frames (cycled if fewer were drawn).</param>
/// <param name="FacesLeft">The art is drawn facing left (the game's art convention is facing right).</param>
/// <param name="Smooth">Scale with smoothing (for painted art) instead of nearest-neighbour (for pixel art).</param>
/// <param name="Problems">Errors, each naming the animation and what to fix. Affected animations use the built-in art.</param>
/// <param name="UsingDefault">Known animations without custom art.</param>
public sealed record SpriteSheetSpec(
    IReadOnlyDictionary<string, IReadOnlyList<SpriteFrame>> Animations,
    bool FacesLeft,
    bool Smooth,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> UsingDefault);

/// <summary>
/// Parses and checks <c>atlas.json</c>. Pure: image sizes come from a callback so this needs no imaging library.
/// <code>
/// {
///   "frameWidth": 32, "frameHeight": 32,   // cell size (default 32×32)
///   "image": "sheet.png",                  // default image
///   "facesLeft": false, "smooth": false,
///   "animations": {
///     "idle": { "row": 0 },                // frames along a row, from column 0; count defaults to the game's
///     "walk": { "row": 1, "column": 2, "frames": 4, "image": "walk.png" }
///   }
/// }
/// </code>
/// </summary>
public static class SpriteSheetParser
{
    public const int DefaultCellSize = 32;

    private static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <param name="json">Contents of atlas.json.</param>
    /// <param name="animations">The game's animations (ids and frame counts).</param>
    /// <param name="imageSize">Size of an image in the sprites folder, or null if it's missing or unreadable.</param>
    public static SpriteSheetSpec Parse(string json, IReadOnlyList<AnimationDef> animations, Func<string, (int Width, int Height)?> imageSize)
    {
        ArgumentNullException.ThrowIfNull(animations);
        ArgumentNullException.ThrowIfNull(imageSize);
        var problems = new List<string>();
        var result = new Dictionary<string, IReadOnlyList<SpriteFrame>>();

        AtlasDto? atlas;
        try
        {
            atlas = JsonSerializer.Deserialize<AtlasDto>(json, Options);
        }
        catch (JsonException e)
        {
            var where = e.LineNumber is { } line ? $" (line {line + 1})" : string.Empty;
            problems.Add($"atlas.json is not valid JSON{where}.");
            atlas = null;
        }

        if (atlas?.Animations is { } defined)
        {
            var known = animations.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
            var sizes = new Dictionary<string, (int Width, int Height)?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (id, anim) in defined)
            {
                if (!known.TryGetValue(id, out var def))
                {
                    problems.Add($"Unknown animation \"{id}\". Known: {string.Join(", ", animations.Select(a => a.Id))}.");
                    continue;
                }

                if (Frames(def, anim ?? new AnimationDto(), atlas, imageSize, sizes, problems) is { } frames)
                {
                    result[def.Id] = frames;
                }
            }
        }

        var builtIn = animations.Where(a => !result.ContainsKey(a.Id)).Select(a => a.Id).ToArray();
        return new SpriteSheetSpec(result, atlas?.FacesLeft ?? false, atlas?.Smooth ?? false, problems, builtIn);
    }

    /// <summary>
    /// The atlas for a single sheet with one row per animation, in the given order, starting at column 0
    /// (the layout of the exported template).
    /// </summary>
    public static string TemplateJson(IReadOnlyList<AnimationDef> animations, string image, int cellSize)
    {
        ArgumentNullException.ThrowIfNull(animations);
        var atlas = new AtlasDto
        {
            FrameWidth = cellSize,
            FrameHeight = cellSize,
            Image = image,
            Animations = animations
                .Select((a, row) => (a, row))
                .ToDictionary(x => x.a.Id, x => (AnimationDto?)new AnimationDto { Row = x.row, Frames = x.a.FrameCount }),
        };
        return JsonSerializer.Serialize(atlas, Options);
    }

    private static List<SpriteFrame>? Frames(
        AnimationDef def, AnimationDto anim, AtlasDto atlas, Func<string, (int Width, int Height)?> imageSize,
        Dictionary<string, (int Width, int Height)?> sizes, List<string> problems)
    {
        var id = def.Id;
        var image = anim.Image ?? atlas.Image;
        if (string.IsNullOrWhiteSpace(image))
        {
            problems.Add($"\"{id}\": no image (set \"image\" on the animation or at the top level).");
            return null;
        }

        if (Path.IsPathRooted(image) || image.Contains("..", StringComparison.Ordinal))
        {
            problems.Add($"\"{id}\": image \"{image}\" must be a file inside the sprites folder.");
            return null;
        }

        if (!sizes.TryGetValue(image, out var size))
        {
            size = imageSize(image);
            sizes[image] = size;
        }

        if (size is not { } imageBounds)
        {
            problems.Add($"\"{id}\": image \"{image}\" is missing or isn't a readable PNG.");
            return null;
        }

        var width = anim.FrameWidth ?? atlas.FrameWidth ?? DefaultCellSize;
        var height = anim.FrameHeight ?? atlas.FrameHeight ?? DefaultCellSize;
        var count = anim.Frames ?? def.FrameCount;
        if (width < 1 || height < 1 || count < 1 || anim.Row < 0 || anim.Column < 0)
        {
            problems.Add($"\"{id}\": frame size, frame count, row and column must be positive.");
            return null;
        }

        var drawn = new List<SpriteFrame>(count);
        for (var i = 0; i < count; i++)
        {
            var rect = RectI.FromSize((anim.Column + i) * width, anim.Row * height, width, height);
            if (rect.Left < 0 || rect.Top < 0 || rect.Right > imageBounds.Width || rect.Bottom > imageBounds.Height)
            {
                problems.Add(
                    $"\"{id}\": frame {i + 1} at ({rect.Left},{rect.Top}) size {width}×{height} doesn't fit in \"{image}\" " +
                    $"({imageBounds.Width}×{imageBounds.Height}).");
                return null;
            }

            drawn.Add(new SpriteFrame(image, rect));
        }

        // The game asks for exactly FrameCount frames: cycle a shorter strip, ignore extras.
        return Enumerable.Range(0, def.FrameCount).Select(i => drawn[i % drawn.Count]).ToList();
    }

    private sealed class AtlasDto
    {
        public int? FrameWidth { get; set; }
        public int? FrameHeight { get; set; }
        public string? Image { get; set; }
        public bool FacesLeft { get; set; }
        public bool Smooth { get; set; }
        public Dictionary<string, AnimationDto?>? Animations { get; set; }
    }

    private sealed class AnimationDto
    {
        public string? Image { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int? Frames { get; set; }
        public int? FrameWidth { get; set; }
        public int? FrameHeight { get; set; }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Traits;
using DesktopPet.Core.Rendering;

namespace DesktopPet.App.SpriteSheet;

/// <summary>One drawable frame and how to draw it.</summary>
/// <param name="Bitmap">The frame.</param>
/// <param name="FacesLeft">The art faces left (the built-in art faces right).</param>
/// <param name="Smooth">Scale with smoothing rather than nearest-neighbour.</param>
internal readonly record struct AtlasFrame(BitmapSource Bitmap, bool FacesLeft, bool Smooth);

/// <summary>The built-in art a pet falls back on: its character, and (for the rabbit) its jacket colours.</summary>
internal readonly record struct BuiltInLook(string Character, JacketShades? Jacket = null);

/// <summary>A sprites folder that was read, and what was found in it (null spec: the folder has no atlas.json).</summary>
internal sealed record SpriteLayer(string Folder, SpriteSheetSpec? Spec);

/// <summary>
/// Maps Core's (animation id, frame index) to frozen bitmaps, one animation at a time from layered sprite folders:
/// the pet's own sprites, then the shared sprites, then the pet's built-in character (DefaultArt). Facing and smoothing are
/// per animation, since each can come from a different atlas.
/// </summary>
internal sealed class SpriteAtlas
{
    public const string AtlasFileName = "atlas.json";

    private readonly Dictionary<string, AtlasFrame[]> _frames;

    private SpriteAtlas(Dictionary<string, AtlasFrame[]> frames, int customCount)
    {
        _frames = frames;
        CustomCount = customCount;
    }

    /// <summary>How many animations use custom art.</summary>
    public int CustomCount { get; }

    public static SpriteAtlas CreateDefault(IEnumerable<AnimationDef> animations, BuiltInLook look) =>
        new(animations.ToDictionary(a => a.Id, a => BuiltIn(a, look)), customCount: 0);

    /// <summary>
    /// Loads <c>atlas.json</c> and its PNGs from each folder, earlier folders winning per animation. Animations no folder
    /// defines (correctly) use the built-in art. Images are read fully into memory so the files stay free to edit.
    /// </summary>
    /// <param name="folders">Sprite folders, most specific first (the pet's own, then the shared one).</param>
    /// <param name="animations">The game's animations.</param>
    /// <param name="look">The pet's built-in art, for animations without custom art.</param>
    public static (SpriteAtlas Atlas, IReadOnlyList<SpriteLayer> Layers) Load(
        IReadOnlyList<string> folders, IReadOnlyList<AnimationDef> animations, BuiltInLook look)
    {
        var layers = new List<(SpriteLayer Layer, Func<string, BitmapSource?> Image)>();
        foreach (var folder in folders)
        {
            layers.Add(LoadLayer(folder, animations));
        }

        var custom = 0;
        var frames = new Dictionary<string, AtlasFrame[]>();
        foreach (var a in animations)
        {
            var source = layers.FirstOrDefault(l => l.Layer.Spec?.Animations.ContainsKey(a.Id) == true);
            if (source.Layer?.Spec is { } spec)
            {
                frames[a.Id] = [.. spec.Animations[a.Id].Select(f => new AtlasFrame(Crop(source.Image(f.Image)!, f.Rect), spec.FacesLeft, spec.Smooth))];
                custom++;
            }
            else
            {
                frames[a.Id] = BuiltIn(a, look);
            }
        }

        return (new SpriteAtlas(frames, custom), [.. layers.Select(l => l.Layer)]);
    }

    public AtlasFrame Get(string animationId, int frameIndex)
    {
        var frames = _frames[animationId];
        return frames[System.Math.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private static (SpriteLayer, Func<string, BitmapSource?>) LoadLayer(string folder, IReadOnlyList<AnimationDef> animations)
    {
        var images = new Dictionary<string, BitmapSource?>(StringComparer.OrdinalIgnoreCase);
        BitmapSource? Image(string name)
        {
            if (!images.TryGetValue(name, out var bitmap))
            {
                bitmap = TryDecode(Path.Combine(folder, name));
                images[name] = bitmap;
            }

            return bitmap;
        }

        var atlasPath = Path.Combine(folder, AtlasFileName);
        if (!File.Exists(atlasPath))
        {
            return (new SpriteLayer(folder, null), Image);
        }

        string json;
        try
        {
            json = File.ReadAllText(atlasPath);
        }
        catch (IOException)
        {
            json = string.Empty; // being written right now; the next change notification will reload
        }

        var spec = SpriteSheetParser.Parse(json, animations, name => Image(name) is { } b ? (b.PixelWidth, b.PixelHeight) : null);
        return (new SpriteLayer(folder, spec), Image);
    }

    private static AtlasFrame[] BuiltIn(AnimationDef animation, BuiltInLook look) =>
        [.. Enumerable.Range(0, animation.FrameCount)
            .Select(i => new AtlasFrame(
                ToBitmap(DefaultArt.Frame(look.Character, animation.Id, i, look.Jacket), DefaultArt.FrameSize), FacesLeft: false, Smooth: false))];

    internal static BitmapFrame? TryDecode(string path)
    {
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException
            or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static CroppedBitmap Crop(BitmapSource image, Core.Math.RectI r)
    {
        var cropped = new CroppedBitmap(image, new Int32Rect(r.Left, r.Top, r.Width, r.Height));
        cropped.Freeze();
        return cropped;
    }

    private static BitmapSource ToBitmap(uint[] argb, int size)
    {
        // 0xAARRGGBB little-endian == B,G,R,A bytes == Bgra32.
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, argb, size * sizeof(uint));
        bitmap.Freeze();
        return bitmap;
    }
}

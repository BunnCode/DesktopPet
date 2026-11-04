using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Rendering;

namespace DesktopPet.App.SpriteSheet;

/// <summary>
/// Maps Core's (animation id, frame index) to frozen bitmaps: custom art from the sprites folder where it's defined,
/// the built-in rabbit (DefaultArt) everywhere else.
/// </summary>
internal sealed class SpriteAtlas
{
    public const string AtlasFileName = "atlas.json";

    private readonly Dictionary<string, BitmapSource[]> _frames;

    private SpriteAtlas(Dictionary<string, BitmapSource[]> frames, bool facesLeft, bool smooth)
    {
        _frames = frames;
        ArtFacesLeft = facesLeft;
        Smooth = smooth;
    }

    /// <summary>The custom art is drawn facing left (the built-in art faces right).</summary>
    public bool ArtFacesLeft { get; }

    /// <summary>Scale with smoothing rather than nearest-neighbour.</summary>
    public bool Smooth { get; }

    public static SpriteAtlas CreateDefault(IEnumerable<AnimationDef> animations, int variant = 0) =>
        new(animations.ToDictionary(a => a.Id, a => BuiltIn(a, variant)), facesLeft: false, smooth: false);

    /// <summary>
    /// Loads <c>atlas.json</c> and its PNGs from <paramref name="folder"/>. Returns the built-in art (and a null spec) if
    /// there's no atlas. Images are read fully into memory so the files stay free to edit.
    /// </summary>
    /// <param name="folder">The sprites folder.</param>
    /// <param name="animations">The game's animations.</param>
    /// <param name="variant">Built-in art colour scheme for animations without custom art (one per pet).</param>
    public static (SpriteAtlas Atlas, SpriteSheetSpec? Spec) Load(string folder, IReadOnlyList<AnimationDef> animations, int variant = 0)
    {
        var atlasPath = Path.Combine(folder, AtlasFileName);
        if (!File.Exists(atlasPath))
        {
            return (CreateDefault(animations, variant), null);
        }

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

        var frames = animations.ToDictionary(
            a => a.Id,
            a => spec.Animations.TryGetValue(a.Id, out var custom)
                ? custom.Select(f => (BitmapSource)Crop(Image(f.Image)!, f.Rect)).ToArray()
                : BuiltIn(a, variant));

        return (new SpriteAtlas(frames, spec.FacesLeft, spec.Smooth), spec);
    }

    public BitmapSource Get(string animationId, int frameIndex)
    {
        var frames = _frames[animationId];
        return frames[System.Math.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private static BitmapSource[] BuiltIn(AnimationDef animation, int variant) =>
        Enumerable.Range(0, animation.FrameCount)
            .Select(i => ToBitmap(DefaultArt.Frame(animation.Id, i, variant), DefaultArt.FrameSize))
            .ToArray();

    private static BitmapFrame? TryDecode(string path)
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

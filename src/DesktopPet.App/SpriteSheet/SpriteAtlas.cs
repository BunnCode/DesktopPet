using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Animation;

namespace DesktopPet.App.SpriteSheet;

/// <summary>Maps Core's (animation id, frame index) to frozen bitmaps. Art source can be swapped later (PNG sheets).</summary>
internal sealed class SpriteAtlas
{
    private readonly Dictionary<string, BitmapSource[]> _frames;

    private SpriteAtlas(Dictionary<string, BitmapSource[]> frames) => _frames = frames;

    public static SpriteAtlas CreatePlaceholder(IEnumerable<AnimationDef> animations)
    {
        var frames = animations.ToDictionary(
            a => a.Id,
            a => Enumerable.Range(0, a.FrameCount)
                .Select(i => ToBitmap(PlaceholderArt.Frame(a.Id, i), PlaceholderArt.FrameSize))
                .ToArray());
        return new SpriteAtlas(frames);
    }

    public BitmapSource Get(string animationId, int frameIndex)
    {
        var frames = _frames[animationId];
        return frames[System.Math.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private static BitmapSource ToBitmap(uint[] argb, int size)
    {
        // 0xAARRGGBB little-endian == B,G,R,A bytes == Bgra32.
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, argb, size * sizeof(uint));
        bitmap.Freeze();
        return bitmap;
    }
}

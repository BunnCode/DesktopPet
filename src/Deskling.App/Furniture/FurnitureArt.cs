using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Deskling.App.SpriteSheet;
using Deskling.Core.Furniture;

namespace Deskling.App.Furniture;

/// <summary>
/// A furniture kind's frames: its image from the furniture folder, cut into <see cref="FurnitureSpec.Frames"/> equal
/// frames side by side. If that image is missing or doesn't divide evenly, the built-in art for that kind (or, for a
/// kind of the user's own, a plain box) stands in and the problem is reported.
/// </summary>
internal static class FurnitureArt
{
    private const string ResourcePrefix = "Deskling.App.Furniture.";

    /// <param name="folder">The furniture folder.</param>
    /// <param name="spec">The definition (its Art, Frames and Kind).</param>
    /// <returns>The frames, and what went wrong (null if nothing).</returns>
    public static (BitmapSource[] Frames, string? Problem) Load(string folder, FurnitureSpec spec)
    {
        var image = SpriteAtlas.TryDecode(Path.Combine(folder, spec.Art));
        string? problem;
        if (image is null)
        {
            problem = $"{spec.Art} couldn't be read";
        }
        else if (image.PixelWidth % spec.Frames != 0)
        {
            problem = $"{spec.Art} is {image.PixelWidth} px wide, which doesn't split into {spec.Frames} equal frames";
        }
        else
        {
            return (Cut(image, spec.Frames), null);
        }

        return BuiltIn(spec) is { } builtIn
            ? (builtIn, problem + " (the built-in art is shown)")
            : ([Placeholder()], problem + " (a plain box is shown)");
    }

    /// <summary>The built-in PNG for a default kind, as written to the furniture folder on first run.</summary>
    public static byte[]? DefaultPng(string kind)
    {
        using var stream = typeof(FurnitureArt).Assembly.GetManifestResourceStream(ResourcePrefix + kind + ".png");
        if (stream is null)
        {
            return null;
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The built-in frames for a kind that has them (the defaults and the egg), or null.</summary>
    public static BitmapSource[]? BuiltIn(FurnitureSpec spec)
    {
        if (DefaultPng(spec.Kind) is not { } png)
        {
            return null;
        }

        using var stream = new MemoryStream(png);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var image = decoder.Frames[0];
        image.Freeze();
        var frames = image.PixelWidth % spec.Frames == 0 ? spec.Frames : 1;
        return Cut(image, frames);
    }

    private static BitmapSource[] Cut(BitmapSource image, int count)
    {
        var width = image.PixelWidth / count;
        var frames = new BitmapSource[count];
        for (var i = 0; i < count; i++)
        {
            var frame = new CroppedBitmap(image, new Int32Rect(i * width, 0, width, image.PixelHeight));
            frame.Freeze();
            frames[i] = frame;
        }

        return frames;
    }

    /// <summary>A grey box with a dark outline, for custom furniture whose art can't be used.</summary>
    private static BitmapSource Placeholder()
    {
        const int size = 8;
        var pixels = new uint[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                pixels[(y * size) + x] = edge ? 0xFF26222B : 0xFF9C98A4;
            }
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * sizeof(uint));
        bitmap.Freeze();
        return bitmap;
    }
}

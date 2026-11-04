using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Death;

namespace DesktopPet.App.Furniture;

/// <summary>
/// A gravestone's picture: the built-in stone art with the pet's name and dates engraved on its face. Drawn at the
/// exact pixel size it's shown at, so the small text stays sharp at any display scaling.
/// </summary>
internal static class GraveArt
{
    // The stone's plain face, in art pixels of grave.png (tools/sprites/make_rabbit.py, grave()).
    private const double FaceLeft = 7;
    private const double FaceTop = 5;
    private const double FaceWidth = 18;
    private const double FaceHeight = 17;

    private const double NameDip = 9;
    private const double DateDip = 6.5;
    private const double MinDip = 5;

    private static readonly Brush Ink = Frozen(Color.FromRgb(0x4A, 0x48, 0x54));
    private static readonly Brush Highlight = Frozen(Color.FromRgb(0xDC, 0xDA, 0xE2));
    private static readonly FontFamily Font = new("Segoe UI");

    /// <param name="grave">Whose gravestone.</param>
    /// <param name="stone">The plain stone (one frame of grave.png).</param>
    /// <param name="size">The size it's shown at, physical pixels.</param>
    /// <param name="scale">The display scale there (DIP to pixels).</param>
    public static BitmapSource Render(GraveInfo grave, BitmapSource stone, (int Width, int Height) size, double scale)
    {
        var (width, height) = (System.Math.Max(1, size.Width), System.Math.Max(1, size.Height));
        var perArtPixel = width / (double)stone.PixelWidth;
        var face = new Rect(FaceLeft * perArtPixel, FaceTop * perArtPixel, FaceWidth * perArtPixel, FaceHeight * perArtPixel);

        var lines = new List<FormattedText> { Line(grave.Name, bold: true, NameDip * scale, face.Width, scale) };
        var culture = CultureInfo.CurrentCulture;
        var died = GraveInfo.Date(grave.DiedUtc, culture);
        if (grave.BornUtc is { } born)
        {
            lines.Add(Line($"{GraveInfo.Date(born, culture)} –", bold: false, DateDip * scale, face.Width, scale));
            lines.Add(Line(died, bold: false, DateDip * scale, face.Width, scale));
        }
        else
        {
            lines.Add(Line($"† {died}", bold: false, DateDip * scale, face.Width, scale));
        }

        var gap = 2 * scale; // between the name and the dates
        var total = lines.Sum(l => l.Height) + gap;
        var y = face.Top + System.Math.Max(0, (face.Height - total) / 2);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(stone, new Rect(0, 0, width, height));
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var x = face.Left + ((face.Width - line.Width) / 2);
                line.SetForegroundBrush(Highlight);
                dc.DrawText(line, new Point(x, y + System.Math.Max(1, scale))); // engraved: a lit lower edge
                line.SetForegroundBrush(Ink);
                dc.DrawText(line, new Point(x, y));
                y += line.Height + (i == 0 ? gap : 0);
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>One line of text, shrunk to fit the face (down to a minimum size), then cut short with "…".</summary>
    private static FormattedText Line(string text, bool bold, double size, double maxWidth, double scale)
    {
        var typeface = new Typeface(Font, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.SemiBold, FontStretches.Normal);
        FormattedText Make(double em) => new(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, em, Ink, null, TextFormattingMode.Display, 1);

        var line = Make(size);
        if (line.Width > maxWidth)
        {
            line = Make(System.Math.Max(MinDip * scale, size * maxWidth / line.Width));
        }

        if (line.Width > maxWidth)
        {
            line.MaxTextWidth = maxWidth;
            line.MaxLineCount = 1;
            line.Trimming = TextTrimming.CharacterEllipsis;
        }

        return line;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

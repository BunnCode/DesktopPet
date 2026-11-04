using DesktopPet.Core.Math;

namespace DesktopPet.Core.Chat;

/// <summary>One line of text recognized on screen, with its bounds in physical pixels.</summary>
public sealed record ScreenLine(string Text, RectI Bounds);

/// <summary>What a pet read on a window: its title and the visible lines near the pet.</summary>
public sealed record ScreenText(string Title, IReadOnlyList<ScreenLine> Lines);

/// <summary>
/// Reads the text shown on a window, as Core sees it. The real one (App) captures the screen and runs the OCR built
/// into Windows; tests use a fake. It returns null when the window may not be read (an excluded app or title), isn't
/// on screen, or reading isn't available.
/// </summary>
public interface IScreenReader
{
    /// <summary>The visible text of <paramref name="windowId"/> around <paramref name="near"/> (the pet's feet).</summary>
    /// <param name="windowId">The window to read.</param>
    /// <param name="near">Where the pet is, physical pixels.</param>
    /// <param name="scale">The DPI scale there (the area read is sized in DIPs).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<ScreenText?> ReadAsync(long windowId, Vec2 near, double scale, CancellationToken cancellationToken);
}

/// <summary>
/// The rules for reading the screen, pure and unit-tested: which windows may be read, which part of a window, which
/// recognized lines count as visible, and the snippet of text a pet is given.
/// </summary>
public static class ScreenReading
{
    /// <summary>The area read around the pet, in DIPs: wide, and reaching further down (text below its feet) than up.</summary>
    public const double AreaHalfWidthDip = 450;

    public const double AreaAboveDip = 200;

    public const double AreaBelowDip = 500;

    /// <summary>
    /// Whether a window may be read: its app (process name, with or without ".exe") isn't excluded and its title
    /// contains none of the excluded words. Both ignore case.
    /// </summary>
    public static bool Allows(string? processName, string? title, ChatRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var app = StripExe(processName ?? string.Empty);
        if (rules.ReadExcludedApps.Any(excluded => string.Equals(StripExe(excluded.Trim()), app, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var name = title ?? string.Empty;
        return !rules.ReadExcludedTitleWords.Any(word =>
            word.Trim().Length > 0 && name.Contains(word.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The part of <paramref name="window"/> to read: a box around the pet (sized in DIPs), slid to stay inside the
    /// window where it can (a pet on a window's top reads further down; one on the taskbar in front of a window reads
    /// further up), then clipped to it.
    /// </summary>
    public static RectI Area(RectI window, Vec2 near, double scale)
    {
        var s = System.Math.Max(0.1, scale);
        var left = Slide(near.X - (AreaHalfWidthDip * s), 2 * AreaHalfWidthDip * s, window.Left, window.Right);
        var top = Slide(near.Y - (AreaAboveDip * s), (AreaAboveDip + AreaBelowDip) * s, window.Top, window.Bottom);
        var box = new RectI(
            (int)System.Math.Floor(left),
            (int)System.Math.Floor(top),
            (int)System.Math.Ceiling(left + (2 * AreaHalfWidthDip * s)),
            (int)System.Math.Ceiling(top + ((AreaAboveDip + AreaBelowDip) * s)));
        return box.Intersect(window);
    }

    /// <summary>Moves a span starting at <paramref name="start"/> into [min, max) as far as it fits.</summary>
    private static double Slide(double start, double length, double min, double max)
    {
        if (start + length > max)
        {
            start = max - length;
        }

        return System.Math.Max(start, min);
    }

    /// <summary>
    /// The lines that are really on <paramref name="window"/>: the centre of each is inside it and not under any of
    /// <paramref name="covers"/> (windows in front of it, our own pets, bubbles and notes included).
    /// </summary>
    public static IReadOnlyList<ScreenLine> Visible(IEnumerable<ScreenLine> lines, RectI window, IReadOnlyList<RectI> covers)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(covers);
        return lines.Where(line =>
        {
            var b = line.Bounds;
            var cx = (b.Left + b.Right) / 2;
            var cy = (b.Top + b.Bottom) / 2;
            var centre = new Vec2(cx, cy);
            return window.Contains(centre) && !covers.Any(c => c.Contains(centre));
        }).ToList();
    }

    /// <summary>
    /// The text a pet is given: the readable lines nearest <paramref name="near"/>, at most
    /// <paramref name="maxChars"/> characters, back in reading order (top to bottom, left to right), one per line.
    /// Lines that are mostly not letters (OCR noise, icons, numbers) are left out, and so is the window's title bar
    /// (a line that's just <paramref name="title"/>; the prompt names the window anyway). "Nearest" counts sideways
    /// distance at half weight: text at the pet's height matters more than text beside it. Null when nothing
    /// readable is left.
    /// </summary>
    public static string? Snippet(IReadOnlyList<ScreenLine> lines, Vec2 near, int maxChars, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var bar = Tidy(title ?? string.Empty);
        var readable = lines
            .Select(l => l with { Text = Tidy(l.Text) })
            .Where(l => IsReadable(l.Text) && !(bar.Length > 0 && string.Equals(l.Text, bar, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(l => Distance(l.Bounds, near))
            .ToList();

        var chosen = new List<ScreenLine>();
        var used = 0;
        foreach (var line in readable)
        {
            var room = maxChars - used - (chosen.Count > 0 ? 1 : 0);
            if (room < 12)
            {
                break;
            }

            var text = line.Text.Length <= room ? line.Text : Cut(line.Text, room);
            if (text.Length == 0)
            {
                break;
            }

            chosen.Add(line with { Text = text });
            used += text.Length + (chosen.Count > 1 ? 1 : 0);
        }

        if (chosen.Count == 0)
        {
            return null;
        }

        var ordered = chosen.OrderBy(l => l.Bounds.Top).ThenBy(l => l.Bounds.Left);
        return string.Join("\n", ordered.Select(l => l.Text));
    }

    /// <summary>A recognized line worth reading: at least 3 letters, and letters at least half of its non-space characters.</summary>
    internal static bool IsReadable(string text)
    {
        var letters = text.Count(char.IsLetter);
        var visible = text.Count(c => !char.IsWhiteSpace(c));
        return letters >= 3 && letters * 2 >= visible;
    }

    private static string Tidy(string text) => string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The start of <paramref name="text"/>, cut at a word boundary to fit <paramref name="room"/>.</summary>
    private static string Cut(string text, int room)
    {
        var cut = text[..room];
        var space = cut.LastIndexOf(' ');
        return (space > room / 2 ? cut[..space] : cut).TrimEnd();
    }

    private static double Distance(RectI box, Vec2 point)
    {
        var dx = 0.5 * System.Math.Max(0, System.Math.Max(box.Left - point.X, point.X - box.Right));
        var dy = System.Math.Max(0, System.Math.Max(box.Top - point.Y, point.Y - box.Bottom));
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
}

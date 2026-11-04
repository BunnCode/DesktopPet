using DesktopPet.Core.Chat;
using DesktopPet.Core.Math;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App.Chat;

/// <summary>
/// Reads a window for a pet (M12b): checks it may be read (<see cref="ScreenReading.Allows"/>), captures the screen
/// around the pet and runs Windows' OCR on it, keeping only the lines that are really on that window. Nothing it reads
/// is logged or kept; the text goes straight into one prompt. Called on the UI thread; the OCR runs on a worker.
/// </summary>
internal sealed class ScreenReader : IScreenReader
{
    private readonly Func<ChatRules> _rules;
    private readonly Func<IReadOnlyList<RectI>> _ownWindows;
    private readonly Action<string, string, bool> _notify;
    private readonly uint _processId = (uint)Environment.ProcessId;
    private TextRecognizer? _ocr;
    private bool _reportedMissing;

    /// <param name="rules">The current chat rules (exclusions).</param>
    /// <param name="ownWindows">Where our own pets, bubbles and notes are, so their text isn't read as the window's.</param>
    /// <param name="notify">Shows a tray notification (title, text, warning).</param>
    public ScreenReader(Func<ChatRules> rules, Func<IReadOnlyList<RectI>> ownWindows, Action<string, string, bool> notify)
    {
        _rules = rules;
        _ownWindows = ownWindows;
        _notify = notify;
    }

    public async Task<ScreenText?> ReadAsync(long windowId, Vec2 near, double scale, CancellationToken cancellationToken)
    {
        _ocr ??= new TextRecognizer();
        if (!_ocr.Available)
        {
            if (!_reportedMissing)
            {
                _reportedMissing = true;
                _notify(
                    "Pets can't read the screen",
                    "Windows has no text recognition (OCR) for your languages. Adding a language with its optical character recognition " +
                    "feature (Settings, Time & language) fixes this.",
                    true);
            }

            return null;
        }

        var (state, bounds) = WindowTracker.Query(windowId);
        if (state != TrackedWindowState.Shown)
        {
            return null;
        }

        var (title, process) = WindowIdentity.Describe(windowId);
        if (!ScreenReading.Allows(process, title, _rules()))
        {
            return null;
        }

        var area = ScreenReading.Area(bounds, near, scale);
        var covers = WindowIdentity.CoversOf(windowId, _processId).Concat(_ownWindows()).ToList();
        if (area.IsEmpty || covers.Any(c => c.Intersect(area) == area))
        {
            return null; // nothing of it showing there
        }

        var pixels = ScreenCapture.Capture(area);
        if (pixels is null)
        {
            return null;
        }

        var lines = await _ocr.RecognizeAsync(pixels, area, scale, cancellationToken).ConfigureAwait(false);
        return new ScreenText(title, ScreenReading.Visible(lines, bounds, covers));
    }
}

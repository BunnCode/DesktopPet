using Deskling.Core.Math;

namespace Deskling.Core.World;

/// <summary>
/// The hard rule for moving other people's windows: a window's visible frame must stay inside the work area.
/// A window already partly outside may be moved back in, never further out; one larger than the work area can't move
/// along that axis at all. Applied by the platform layer against the window's live position right before every move,
/// so no simulation bug or stale snapshot can push a window off screen.
/// </summary>
public static class WindowMoveGuard
{
    public static (int Dx, int Dy) Clamp(RectI frame, RectI workArea, int dx, int dy) =>
        (ClampAxis(frame.Left, frame.Right, workArea.Left, workArea.Right, dx),
         ClampAxis(frame.Top, frame.Bottom, workArea.Top, workArea.Bottom, dy));

    private static int ClampAxis(int low, int high, int min, int max, int delta)
    {
        if (delta > 0)
        {
            return System.Math.Min(delta, System.Math.Max(0, max - high));
        }

        if (delta < 0)
        {
            return -System.Math.Min(-delta, System.Math.Max(0, low - min));
        }

        return 0;
    }
}

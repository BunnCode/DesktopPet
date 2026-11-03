using DesktopPet.Core.Animation;

namespace DesktopPet.App.SpriteSheet;

/// <summary>
/// Procedurally generated 32×32 pixel-art blob, so we have something to look at before real art exists.
/// Pixels are 0xAARRGGBB; everything outside the shape is alpha 0 so clicks pass through.
/// The body always rests on row 29 with feet below it, so the sprite's bottom edge is the feet line.
/// </summary>
internal static class PlaceholderArt
{
    public const int FrameSize = 32;

    // Unpainted pixels stay 0 (fully transparent).
    private const uint Body = 0xFFF2A65A;
    private const uint BodyLight = 0xFFF8C98E;
    private const uint Outline = 0xFF7A3E14;
    private const uint Feet = 0xFF7A3E14;
    private const uint Eye = 0xFF1E1A1A;
    private const uint EyeShine = 0xFFFFFFFF;
    private const uint Blush = 0xFFEE8A7A;

    private enum Eyes
    {
        Open,
        Blink,
        Wide,
        Squeezed,
    }

    private enum FeetPose
    {
        Standing,
        DangleLeft,
        DangleRight,
    }

    /// <param name="Top">Top row of the body ellipse (body bottom is fixed at row 29).</param>
    /// <param name="HalfWidth">Horizontal radius of the body.</param>
    private readonly record struct Pose(double Top, double HalfWidth, Eyes Eyes, FeetPose Feet, bool Mouth = false);

    public static uint[] Frame(string animationId, int frameIndex) => Draw(PoseFor(animationId, frameIndex));

    public static uint[] TrayIconPixels() => Draw(PoseFor(Animations.Idle.Id, 0));

    private static Pose PoseFor(string animationId, int frame)
    {
        if (animationId == Animations.Fall.Id)
        {
            // Stretched tall and narrow, eyes wide, feet fluttering.
            return new Pose(4, 12, Eyes.Wide, frame % 2 == 0 ? FeetPose.DangleLeft : FeetPose.DangleRight, Mouth: true);
        }

        if (animationId == Animations.Land.Id)
        {
            // Big squash on impact, recovering to the idle shape.
            return frame switch
            {
                0 => new Pose(13, 15, Eyes.Squeezed, FeetPose.Standing),
                1 => new Pose(10, 14.2, Eyes.Squeezed, FeetPose.Standing),
                2 => new Pose(8, 13.5, Eyes.Open, FeetPose.Standing),
                _ => new Pose(6, 12.6, Eyes.Open, FeetPose.Standing),
            };
        }

        // Idle (16 frames): breathe in (1px squash) on frames 4–7 and 12–15, blink on 13.
        var squash = (frame / 4) % 2 == 1;
        return new Pose(squash ? 8 : 7, 13, frame == 13 ? Eyes.Blink : Eyes.Open, FeetPose.Standing);
    }

    private static uint[] Draw(Pose pose)
    {
        const int size = FrameSize;
        const double bottom = 29;
        var px = new uint[size * size];
        var inside = new bool[size * size];

        double top = pose.Top;
        double cx = size / 2.0, cy = (top + bottom) / 2.0;
        double rx = pose.HalfWidth, ry = (bottom - top) / 2.0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5 - cx) / rx;
                var dy = (y + 0.5 - cy) / ry;
                inside[(y * size) + x] = (dx * dx) + (dy * dy) <= 1.0;
            }
        }

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!inside[(y * size) + x])
                {
                    continue;
                }

                var edge = x == 0 || y == 0 || x == size - 1 || y == size - 1
                    || !inside[(y * size) + x - 1] || !inside[(y * size) + x + 1]
                    || !inside[((y - 1) * size) + x] || !inside[((y + 1) * size) + x];

                // Soft highlight in the upper-left.
                var hx = (x + 0.5 - (cx - (rx * 0.4))) / (rx * 0.38);
                var hy = (y + 0.5 - (top + (ry * 0.45))) / (ry * 0.3);
                var highlight = (hx * hx) + (hy * hy) <= 1.0;

                px[(y * size) + x] = edge ? Outline : highlight ? BodyLight : Body;
            }
        }

        DrawFeet(px, pose.Feet);
        DrawFace(px, pose, (int)cy);
        return px;
    }

    private static void DrawFeet(uint[] px, FeetPose feet)
    {
        switch (feet)
        {
            case FeetPose.Standing:
                FillRect(px, 9, 29, 6, 2, Feet);
                FillRect(px, 17, 29, 6, 2, Feet);
                FillRect(px, 10, 31, 4, 1, Feet);
                FillRect(px, 18, 31, 4, 1, Feet);
                break;
            case FeetPose.DangleLeft:
                FillRect(px, 10, 29, 4, 3, Feet);
                FillRect(px, 18, 29, 4, 2, Feet);
                break;
            case FeetPose.DangleRight:
                FillRect(px, 10, 29, 4, 2, Feet);
                FillRect(px, 18, 29, 4, 3, Feet);
                break;
        }
    }

    private static void DrawFace(uint[] px, Pose pose, int centreY)
    {
        var eyeY = centreY - 1;
        switch (pose.Eyes)
        {
            case Eyes.Open:
                FillRect(px, 11, eyeY - 1, 2, 3, Eye);
                FillRect(px, 19, eyeY - 1, 2, 3, Eye);
                SetPixel(px, 11, eyeY - 1, EyeShine);
                SetPixel(px, 19, eyeY - 1, EyeShine);
                break;
            case Eyes.Blink:
                FillRect(px, 10, eyeY + 1, 3, 1, Eye);
                FillRect(px, 19, eyeY + 1, 3, 1, Eye);
                break;
            case Eyes.Wide:
                FillRect(px, 10, eyeY - 2, 3, 4, Eye);
                FillRect(px, 19, eyeY - 2, 3, 4, Eye);
                SetPixel(px, 10, eyeY - 2, EyeShine);
                SetPixel(px, 19, eyeY - 2, EyeShine);
                break;
            case Eyes.Squeezed: // > <
                SetPixel(px, 10, eyeY - 1, Eye);
                SetPixel(px, 11, eyeY, Eye);
                SetPixel(px, 10, eyeY + 1, Eye);
                SetPixel(px, 21, eyeY - 1, Eye);
                SetPixel(px, 20, eyeY, Eye);
                SetPixel(px, 21, eyeY + 1, Eye);
                break;
        }

        if (pose.Mouth)
        {
            FillRect(px, 15, eyeY + 4, 2, 2, Eye);
        }

        FillRect(px, 8, eyeY + 3, 2, 1, Blush);
        FillRect(px, 22, eyeY + 3, 2, 1, Blush);
    }

    private static void SetPixel(uint[] px, int x, int y, uint color) => FillRect(px, x, y, 1, 1, color);

    private static void FillRect(uint[] px, int x, int y, int w, int h, uint color)
    {
        for (var yy = System.Math.Max(0, y); yy < System.Math.Min(FrameSize, y + h); yy++)
        {
            for (var xx = System.Math.Max(0, x); xx < System.Math.Min(FrameSize, x + w); xx++)
            {
                px[(yy * FrameSize) + xx] = color;
            }
        }
    }
}

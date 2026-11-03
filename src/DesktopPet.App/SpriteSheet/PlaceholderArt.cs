using DesktopPet.Core.Animation;

namespace DesktopPet.App.SpriteSheet;

/// <summary>
/// Procedurally generated 32×32 pixel-art blob, so we have something to look at before real art exists.
/// Pixels are 0xAARRGGBB; everything outside the shape is alpha 0 so clicks pass through.
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

    public static uint[] Frame(string animationId, int frameIndex) => animationId switch
    {
        _ when animationId == Animations.Idle.Id => Idle(frameIndex),
        _ => Idle(0),
    };

    public static uint[] TrayIconPixels() => Idle(0);

    /// <summary>16 frames: breathe in (body squashes 1px) on frames 4–7 and 12–15, blink on 13.</summary>
    private static uint[] Idle(int frame)
    {
        var squash = (frame / 4) % 2 == 1 ? 1 : 0;
        var blink = frame == 13;
        return DrawBlob(squash, blink);
    }

    private static uint[] DrawBlob(int squash, bool blink)
    {
        const int size = FrameSize;
        var px = new uint[size * size];
        var inside = new bool[size * size];

        // Body: ellipse resting on the feet. Squash lowers the top edge by 1px.
        double top = 7 + squash, bottom = 29;
        double cx = size / 2.0, cy = (top + bottom) / 2.0;
        double rx = 13.0, ry = (bottom - top) / 2.0;

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
                var hx = (x + 0.5 - (cx - 5)) / 5.0;
                var hy = (y + 0.5 - (top + 5)) / 3.5;
                var highlight = (hx * hx) + (hy * hy) <= 1.0;

                px[(y * size) + x] = edge ? Outline : highlight ? BodyLight : Body;
            }
        }

        // Feet: two little ovals under the body.
        FillRect(px, 9, 29, 6, 2, Feet);
        FillRect(px, 17, 29, 6, 2, Feet);
        FillRect(px, 10, 31, 4, 1, Feet);
        FillRect(px, 18, 31, 4, 1, Feet);

        // Face.
        var eyeY = (int)cy - 1;
        if (blink)
        {
            FillRect(px, 10, eyeY + 1, 3, 1, Eye);
            FillRect(px, 19, eyeY + 1, 3, 1, Eye);
        }
        else
        {
            FillRect(px, 11, eyeY - 1, 2, 3, Eye);
            FillRect(px, 19, eyeY - 1, 2, 3, Eye);
            px[((eyeY - 1) * size) + 11] = EyeShine;
            px[((eyeY - 1) * size) + 19] = EyeShine;
        }

        FillRect(px, 8, eyeY + 3, 2, 1, Blush);
        FillRect(px, 22, eyeY + 3, 2, 1, Blush);

        return px;
    }

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

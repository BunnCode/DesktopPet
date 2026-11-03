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
    private const uint Apple = 0xFFD8352B;
    private const uint AppleShine = 0xFFF27A6E;
    private const uint Leaf = 0xFF4CAF50;
    private const uint Snooze = 0xFF9FC5FF;

    private enum Eyes
    {
        Open,
        Blink,
        Wide,
        Squeezed,
        Happy,

        /// <summary>Half-closed, lids sloping down at the outer corners: sad.</summary>
        Droopy,
    }

    private enum Food
    {
        None,
        Whole,
        Bitten,
    }

    private enum FeetPose
    {
        /// <summary>No feet under the body (drawn as <see cref="Limbs"/> instead).</summary>
        None,
        Standing,
        DangleLeft,
        DangleRight,
        StepLeft,
        StepRight,
        Sitting,
    }

    /// <param name="Top">Top row of the body ellipse (body bottom is fixed at row 29).</param>
    /// <param name="HalfWidth">Horizontal radius of the body.</param>
    /// <param name="Eyes">Eye style.</param>
    /// <param name="Feet">Feet style.</param>
    /// <param name="Mouth">Small open mouth.</param>
    /// <param name="Look">Horizontal eye offset; positive looks right (the art's facing direction).</param>
    /// <param name="Smile">Small smile under the eyes.</param>
    /// <param name="Arms">Arms raised to grip something at the top edge of the sprite.</param>
    /// <param name="Frown">Small frown under the eyes.</param>
    /// <param name="Food">Snack held in front.</param>
    /// <param name="Zzz">0 none, 1 small "z", 2 bigger "z" drifting higher.</param>
    /// <param name="Bottom">Bottom row of the body ellipse.</param>
    /// <param name="ShiftX">Shifts the body, face and feet horizontally (leaning away from a wall).</param>
    /// <param name="Limbs">Wall/ledge limbs drawn after the shift, at fixed positions.</param>
    /// <param name="LimbPhase">Which of the two alternating limb positions.</param>
    private readonly record struct Pose(
        double Top, double HalfWidth, Eyes Eyes, FeetPose Feet, bool Mouth = false, int Look = 0, bool Smile = false,
        Arms Arms = Arms.None, bool Frown = false, Food Food = Food.None, int Zzz = 0,
        double Bottom = 29, int ShiftX = 0, Limbs Limbs = Limbs.None, int LimbPhase = 0);

    private enum Limbs
    {
        None,

        /// <summary>Hands and feet on a wall at the sprite's right edge, alternating for hand-over-hand climbing.</summary>
        Climb,

        /// <summary>Both hands pressed against a wall at the right edge.</summary>
        Push,

        /// <summary>Legs dangling below the body over a ledge, swinging.</summary>
        Dangle,
    }

    private enum Arms
    {
        None,

        /// <summary>Both hands gripping at row 0.</summary>
        Up,

        /// <summary>Front (right) hand reaching ahead and slightly lower: mid hand-over-hand.</summary>
        ReachFront,

        /// <summary>Back (left) hand catching up.</summary>
        ReachBack,
    }

    public static uint[] Frame(string animationId, int frameIndex) => Draw(PoseFor(animationId, frameIndex));

    public static uint[] TrayIconPixels() => Draw(PoseFor(Animations.Idle.Id, 0));

    private static Pose PoseFor(string animationId, int frame)
    {
        if (animationId == Animations.Climb.Id)
        {
            // Body leaning back from a wall on the right, hands and feet on it, alternating.
            return new Pose(4, 11.5, Eyes.Open, FeetPose.None, Look: 1, Bottom: 28, ShiftX: -4, Limbs: Limbs.Climb, LimbPhase: frame);
        }

        if (animationId == Animations.Cling.Id)
        {
            // Holding still on the wall, blinking now and then.
            return new Pose(
                4, 11.5, frame == 0 ? Eyes.Open : Eyes.Blink, FeetPose.None, Look: 1, Bottom: 28, ShiftX: -4, Limbs: Limbs.Climb);
        }

        if (animationId == Animations.Push.Id)
        {
            // Leaning into the window, hands on it, straining, feet shuffling.
            var feet = (frame % 4) switch { 0 => FeetPose.StepLeft, 2 => FeetPose.StepRight, _ => FeetPose.Standing };
            return new Pose(8, 12.5, Eyes.Squeezed, feet, ShiftX: -2, Limbs: Limbs.Push);
        }

        if (animationId == Animations.SitLedge.Id)
        {
            // Perched on a title bar: body ends at row 23 (the ledge, see Animations.LedgeSinkFraction), legs swinging below.
            return new Pose(5, 13, Eyes.Happy, FeetPose.None, Smile: true, Bottom: 23, Limbs: Limbs.Dangle, LimbPhase: frame);
        }

        if (animationId == Animations.IdleSad.Id)
        {
            // Slumped, droopy-eyed, frowning; slow breathing.
            return new Pose(frame == 0 ? 9 : 10, 13.4, Eyes.Droopy, FeetPose.Standing, Frown: true);
        }

        if (animationId == Animations.Sleep.Id)
        {
            // Curled up low, eyes shut, a "z" drifting up.
            return new Pose(frame == 0 ? 11 : 12, 14.4, Eyes.Blink, FeetPose.Sitting, Zzz: frame + 1);
        }

        if (animationId == Animations.Eat.Id)
        {
            // Happily munching an apple held out front.
            return new Pose(
                8, 13, Eyes.Happy, FeetPose.Standing, Mouth: frame == 0, Smile: frame == 1,
                Food: frame == 0 ? Food.Whole : Food.Bitten);
        }

        if (animationId == Animations.Fall.Id)
        {
            // Stretched tall and narrow, eyes wide, feet fluttering.
            return new Pose(4, 12, Eyes.Wide, frame % 2 == 0 ? FeetPose.DangleLeft : FeetPose.DangleRight, Mouth: true);
        }

        if (animationId == Animations.Hang.Id)
        {
            // Dangling from both hands, a slow sway of the feet.
            return new Pose(6, 12, Eyes.Open, frame == 0 ? FeetPose.DangleLeft : FeetPose.DangleRight, Arms: Arms.Up);
        }

        if (animationId == Animations.Shimmy.Id)
        {
            return new Pose(
                6, 12, Eyes.Open, frame == 0 ? FeetPose.DangleLeft : FeetPose.DangleRight, Look: 1,
                Arms: frame == 0 ? Arms.ReachFront : Arms.ReachBack);
        }

        if (animationId == Animations.Held.Id)
        {
            // Scrunched-eyed and protesting, feet kicking.
            return new Pose(5, 12, Eyes.Squeezed, frame % 2 == 0 ? FeetPose.DangleLeft : FeetPose.DangleRight, Mouth: true);
        }

        if (animationId == Animations.Happy.Id)
        {
            // Squash-stretch wiggle with closed happy eyes and a smile.
            return frame switch
            {
                0 => new Pose(9, 14, Eyes.Happy, FeetPose.Standing, Smile: true),
                1 => new Pose(5, 12, Eyes.Happy, FeetPose.Standing, Smile: true),
                2 => new Pose(9, 14, Eyes.Happy, FeetPose.Standing, Smile: true),
                _ => new Pose(7, 13, Eyes.Happy, FeetPose.Standing, Smile: true),
            };
        }

        if (animationId == Animations.Walk.Id)
        {
            // Waddle: step, pass (bob up), other step, pass. Eyes look where it's going.
            return frame switch
            {
                0 => new Pose(8, 13, Eyes.Open, FeetPose.StepLeft, Look: 1),
                1 => new Pose(7, 12.6, Eyes.Open, FeetPose.Standing, Look: 1),
                2 => new Pose(8, 13, Eyes.Open, FeetPose.StepRight, Look: 1),
                _ => new Pose(7, 12.6, Eyes.Open, FeetPose.Standing, Look: 1),
            };
        }

        if (animationId == Animations.Sit.Id)
        {
            // Plopped down wide and low, eyes closed happily, gentle sway.
            return new Pose(frame == 0 ? 11 : 12, frame == 0 ? 14.2 : 14.6, Eyes.Happy, FeetPose.Sitting);
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
        var bottom = pose.Bottom;
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
        DrawArms(px, pose.Arms, (int)top);
        DrawFace(px, pose, (int)cy);
        DrawFood(px, pose.Food);
        DrawZzz(px, pose.Zzz);

        if (pose.ShiftX != 0)
        {
            px = Shifted(px, pose.ShiftX);
        }

        if (pose.Limbs == Limbs.None)
        {
            return px;
        }

        // Limbs go underneath the body so they emerge from it rather than floating beside it.
        var layered = new uint[px.Length];
        DrawLimbs(layered, pose.Limbs, pose.LimbPhase, (int)bottom);
        for (var i = 0; i < px.Length; i++)
        {
            if (px[i] != 0)
            {
                layered[i] = px[i];
            }
        }

        return layered;
    }

    private static uint[] Shifted(uint[] px, int dx)
    {
        var result = new uint[px.Length];
        for (var y = 0; y < FrameSize; y++)
        {
            for (var x = 0; x < FrameSize; x++)
            {
                var from = x - dx;
                if (from >= 0 && from < FrameSize)
                {
                    result[(y * FrameSize) + x] = px[(y * FrameSize) + from];
                }
            }
        }

        return result;
    }

    /// <summary>Limbs reaching a wall at the sprite's right edge (x = 31), or legs dangling below the body.</summary>
    private static void DrawLimbs(uint[] px, Limbs limbs, int phase, int bodyBottom)
    {
        switch (limbs)
        {
            case Limbs.Climb:
                // Upper hand and lower foot move together, then the other pair: hand-over-hand.
                var (handA, handB) = phase == 0 ? (6, 13) : (10, 5);
                var (footA, footB) = phase == 0 ? (25, 19) : (21, 26);
                Limb(px, y: handA, padHeight: 3);
                Limb(px, y: handB, padHeight: 3);
                Limb(px, y: footA, padHeight: 4);
                Limb(px, y: footB, padHeight: 4);
                break;

            case Limbs.Push:
                Limb(px, y: 14, padHeight: 3);
                Limb(px, y: 20, padHeight: 3);
                break;

            case Limbs.Dangle:
                // Two stubby legs hanging over the ledge, swinging slightly between frames.
                var swing = phase == 0 ? 0 : 1;
                FillRect(px, 10, bodyBottom - 2, 3, 6, Outline);
                FillRect(px, 19, bodyBottom - 2, 3, 6, Outline);
                FillRect(px, 9 - swing, bodyBottom + 3, 5, 3, Feet);
                FillRect(px, 18 + swing, bodyBottom + 3, 5, 3, Feet);
                break;
        }
    }

    /// <summary>A 2 px thick limb from inside the body out to a pad touching the wall at the right edge.</summary>
    private static void Limb(uint[] px, int y, int padHeight)
    {
        FillRect(px, 12, y, 17, 2, Outline);
        FillRect(px, 28, y - ((padHeight - 2) / 2), 4, padHeight, Feet);
    }

    /// <summary>An apple held out in front (the art faces right), with a bite out of it once eaten.</summary>
    private static void DrawFood(uint[] px, Food food)
    {
        if (food == Food.None)
        {
            return;
        }

        FillRect(px, 24, 21, 5, 6, Apple);
        FillRect(px, 23, 22, 7, 4, Apple);
        SetPixel(px, 25, 22, AppleShine);
        SetPixel(px, 26, 20, Outline); // stem
        FillRect(px, 27, 19, 2, 1, Leaf);

        if (food == Food.Bitten)
        {
            // Bite on the side facing the mouth. The apple overlaps the body, so the bite shows body, not a
            // transparent hole through the pet.
            FillRect(px, 23, 22, 2, 2, Body);
            SetPixel(px, 24, 21, Body);
        }
    }

    /// <summary>A pixel "z" over the head; the second frame is bigger and higher, so it seems to drift up.</summary>
    private static void DrawZzz(uint[] px, int zzz)
    {
        if (zzz == 1)
        {
            // 4 wide so the diagonal actually slants (3 wide reads as an "I").
            FillRect(px, 23, 5, 4, 1, Snooze);
            SetPixel(px, 25, 6, Snooze);
            SetPixel(px, 24, 7, Snooze);
            FillRect(px, 23, 8, 4, 1, Snooze);
        }
        else if (zzz == 2)
        {
            FillRect(px, 25, 1, 5, 1, Snooze);
            SetPixel(px, 28, 2, Snooze);
            SetPixel(px, 27, 3, Snooze);
            SetPixel(px, 26, 4, Snooze);
            FillRect(px, 25, 5, 5, 1, Snooze);
        }
    }

    /// <summary>Arms from the shoulders up to hands at the very top rows (where the sprite's top meets the bar).</summary>
    private static void DrawArms(uint[] px, Arms arms, int bodyTop)
    {
        if (arms == Arms.None)
        {
            return;
        }

        // (arm column, hand top row) for the back (left) and front (right) hands.
        var (backHand, frontHand) = arms switch
        {
            Arms.ReachFront => (0, 1),
            Arms.ReachBack => (1, 0),
            _ => (0, 0),
        };
        var frontX = arms == Arms.ReachFront ? 22 : 20;
        DrawArm(px, 9, backHand, bodyTop);
        DrawArm(px, frontX, frontHand, bodyTop);
    }

    private static void DrawArm(uint[] px, int x, int handTop, int bodyTop)
    {
        FillRect(px, x, handTop + 1, 2, bodyTop + 3 - (handTop + 1), Outline); // arm
        FillRect(px, x - 1, handTop, 4, 2, Feet);                              // hand gripping
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
            case FeetPose.StepLeft: // back foot planted, front foot lifted and forward
                FillRect(px, 8, 29, 6, 2, Feet);
                FillRect(px, 9, 31, 4, 1, Feet);
                FillRect(px, 19, 29, 6, 2, Feet);
                break;
            case FeetPose.StepRight: // the other foot planted
                FillRect(px, 10, 29, 6, 2, Feet);
                FillRect(px, 18, 29, 6, 2, Feet);
                FillRect(px, 19, 31, 4, 1, Feet);
                break;
            case FeetPose.Sitting: // splayed out flat to the sides
                FillRect(px, 4, 30, 6, 2, Feet);
                FillRect(px, 22, 30, 6, 2, Feet);
                break;
        }
    }

    private static void DrawFace(uint[] px, Pose pose, int centreY)
    {
        var eyeY = centreY - 1;
        var look = pose.Look;
        switch (pose.Eyes)
        {
            case Eyes.Open:
                FillRect(px, 11 + look, eyeY - 1, 2, 3, Eye);
                FillRect(px, 19 + look, eyeY - 1, 2, 3, Eye);
                SetPixel(px, 11 + look + (look > 0 ? 1 : 0), eyeY - 1, EyeShine);
                SetPixel(px, 19 + look + (look > 0 ? 1 : 0), eyeY - 1, EyeShine);
                break;
            case Eyes.Droopy: // heavy lids sloping down to the outside
                FillRect(px, 11, eyeY, 2, 2, Eye);
                FillRect(px, 19, eyeY, 2, 2, Eye);
                FillRect(px, 10, eyeY - 1, 3, 1, Outline);
                SetPixel(px, 9, eyeY, Outline);
                FillRect(px, 19, eyeY - 1, 3, 1, Outline);
                SetPixel(px, 22, eyeY, Outline);
                break;
            case Eyes.Happy: // ^ ^
                SetPixel(px, 10, eyeY, Eye);
                SetPixel(px, 11, eyeY - 1, Eye);
                SetPixel(px, 12, eyeY, Eye);
                SetPixel(px, 19, eyeY, Eye);
                SetPixel(px, 20, eyeY - 1, Eye);
                SetPixel(px, 21, eyeY, Eye);
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

        if (pose.Frown)
        {
            SetPixel(px, 14, eyeY + 5, Eye);
            FillRect(px, 15, eyeY + 4, 2, 1, Eye);
            SetPixel(px, 17, eyeY + 5, Eye);
        }

        if (pose.Smile)
        {
            SetPixel(px, 14, eyeY + 3, Eye);
            FillRect(px, 15, eyeY + 4, 2, 1, Eye);
            SetPixel(px, 17, eyeY + 3, Eye);
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

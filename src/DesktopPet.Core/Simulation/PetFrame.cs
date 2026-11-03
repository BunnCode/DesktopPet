using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;

namespace DesktopPet.Core.Simulation;

/// <summary>Everything a renderer needs to draw the pet for one simulation step.</summary>
/// <param name="FeetPos">Bottom-centre of the pet, physical pixels.</param>
/// <param name="Size">Physical size of the pet's sprite box.</param>
/// <param name="FacingLeft">Whether to mirror the sprite horizontally (art faces right).</param>
/// <param name="AnimationId">Animation to draw.</param>
/// <param name="FrameIndex">Frame within that animation.</param>
/// <param name="State">Current behavior, for debugging and UI.</param>
/// <param name="SinkY">
/// Draw the sprite this many pixels below the feet point (e.g. sitting on a ledge with legs hanging over it).
/// </param>
public readonly record struct PetFrame(
    Vec2 FeetPos, Vec2 Size, bool FacingLeft, string AnimationId, int FrameIndex, PetStateId State, double SinkY = 0)
{
    public RectI Bounds => SpriteRect(FeetPos + new Vec2(0, SinkY), Size);

    /// <summary>Sprite rectangle whose bottom edge sits on <paramref name="feet"/>.Y, centred on <paramref name="feet"/>.X.</summary>
    public static RectI SpriteRect(Vec2 feet, Vec2 size)
    {
        var width = (int)System.Math.Round(size.X);
        var height = (int)System.Math.Round(size.Y);
        var left = (int)System.Math.Round(feet.X - (size.X / 2));
        var bottom = (int)System.Math.Round(feet.Y);
        return new RectI(left, bottom - height, left + width, bottom);
    }
}

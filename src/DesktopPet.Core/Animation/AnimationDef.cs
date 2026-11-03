namespace DesktopPet.Core.Animation;

/// <summary>Timing description of an animation. Bitmaps are the renderer's business; Core only deals in ids and frame indices.</summary>
public sealed record AnimationDef
{
    public AnimationDef(string id, int frameCount, double fps, bool loop)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(frameCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fps);
        Id = id;
        FrameCount = frameCount;
        Fps = fps;
        Loop = loop;
    }

    public string Id { get; }
    public int FrameCount { get; }
    public double Fps { get; }
    public bool Loop { get; }

    public double Duration => FrameCount / Fps;
}

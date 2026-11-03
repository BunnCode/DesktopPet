using DesktopPet.Core.Animation;
using DesktopPet.Core.Math;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Simulation;

/// <summary>
/// The whole pet, stepped at a fixed rate. M1: stands at the bottom-centre of the primary
/// work area and plays the idle animation. Physics, behaviors and stats arrive in later milestones.
/// </summary>
public sealed class PetSimulation
{
    private readonly PetConfig _config;
    private readonly AnimationPlayer _animation = new(Animations.Idle);
    private readonly Vec2 _feet;
    private readonly double _scale;

    public PetSimulation(PetConfig config, IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(monitors);
        _config = config;
        (_feet, _scale) = Spawn(monitors);
        Current = BuildFrame();
        Previous = Current;
    }

    /// <summary>State after the latest step.</summary>
    public PetFrame Current { get; private set; }

    /// <summary>State before the latest step; renderers interpolate between this and <see cref="Current"/>.</summary>
    public PetFrame Previous { get; private set; }

    public void Tick(double dt)
    {
        Previous = Current;
        _animation.Update(dt);
        Current = BuildFrame();
    }

    internal static (Vec2 Feet, double Scale) Spawn(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            return (new Vec2(100, 100), 1.0);
        }

        var monitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        var area = monitor.WorkArea;
        return (new Vec2(area.Left + (area.Width / 2.0), area.Bottom), monitor.Scale);
    }

    private PetFrame BuildFrame()
    {
        var size = _config.SizeDip * _scale;
        return new PetFrame(_feet, new Vec2(size, size), FacingLeft: false, _animation.Current.Id, _animation.FrameIndex);
    }
}

using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Behavior;

/// <summary>Shared mutable state that behavior states read and act on. One per pet.</summary>
internal sealed class PetContext
{
    public PetContext(PetConfig config, IRandom random)
    {
        Config = config;
        Random = random;
    }

    public PetConfig Config { get; }
    public IRandom Random { get; }
    public PetBody Body { get; } = new();
    public AnimationPlayer Animation { get; } = new(Animations.Fall);

    public IReadOnlyList<MonitorInfo> Monitors { get; set; } = [];

    /// <summary>DPI scale of the monitor the pet is on.</summary>
    public double Scale { get; set; } = 1.0;

    public bool FacingLeft { get; set; }

    public double SizePx => Config.SizeDip * Scale;
    public double HalfWidthPx => SizePx * Config.BodyHalfWidthFraction;

    /// <summary>+1 facing right, -1 facing left.</summary>
    public int Direction => FacingLeft ? -1 : 1;
}

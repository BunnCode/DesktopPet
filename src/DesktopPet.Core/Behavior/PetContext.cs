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
        PushingEnabled = config.EnablePushing;
    }

    /// <summary>Current tuning; replaced live when the settings file changes.</summary>
    public PetConfig Config { get; set; }
    public IRandom Random { get; }
    public PetBody Body { get; } = new();
    public AnimationPlayer Animation { get; } = new(Animations.Fall);

    public IReadOnlyList<MonitorInfo> Monitors { get; set; } = [];

    public IReadOnlyList<Platform> Platforms { get; set; } = [];

    public IReadOnlyList<Wall> Walls { get; set; } = [];

    /// <summary>While climbing: -1 up, +1 down.</summary>
    public int ClimbDirection { get; set; } = -1;

    /// <summary>Whether pushing windows is allowed right now (starts from config; the user can switch it off at runtime).</summary>
    public bool PushingEnabled { get; set; }

    /// <summary>The wall a push was started against.</summary>
    public string? PushWallKey { get; set; }

    /// <summary>Windows that didn't budge when pushed (e.g. elevated apps); not pushed again this session.</summary>
    public HashSet<long> ImmovableWindows { get; } = [];

    /// <summary>Requests for the host to move real windows, drained each frame.</summary>
    public List<Simulation.WindowCommand> Commands { get; } = [];

    /// <summary>The wall the body is clinging to (or pushing), if any.</summary>
    public Wall? CurrentWall(string? key = null) =>
        Physics.PhysicsSystem.FindWall(Walls, key ?? Body.GroundKey, Body.Feet.Y - (SizePx / 2));

    /// <summary>The ground platform segment under the feet, if grounded.</summary>
    public Platform? CurrentGround()
    {
        foreach (var p in Platforms)
        {
            if (p.Key == Body.GroundKey && p.SpansX(Body.Feet.X))
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>DPI scale of the monitor the pet is on.</summary>
    public double Scale { get; set; } = 1.0;

    public bool FacingLeft { get; set; }

    public Stats.PetStats Stats { get; set; } = Core.Stats.PetStats.Fresh;

    /// <summary>Set by the menu's "Sleep": sleep until rested rather than a short nap.</summary>
    public bool TuckedIn { get; set; }

    public double SizePx => Config.SizeDip * Scale;
    public double HalfWidthPx => SizePx * Config.BodyHalfWidthFraction;

    /// <summary>+1 facing right, -1 facing left.</summary>
    public int Direction => FacingLeft ? -1 : 1;
}

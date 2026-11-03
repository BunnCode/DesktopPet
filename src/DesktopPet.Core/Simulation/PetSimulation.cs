using DesktopPet.Core.Animation;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Simulation;

/// <summary>
/// The whole pet, stepped at a fixed rate against the latest desktop snapshot.
/// M2: spawns at the top of the primary monitor, falls and lands on work-area floors, respawns if lost.
/// Animation selection here is a stopgap until the behavior state machine (M3).
/// </summary>
public sealed class PetSimulation
{
    private readonly PetConfig _config;
    private readonly PetBody _body = new();
    private readonly AnimationPlayer _animation = new(Animations.Fall);

    private DesktopSnapshot _snapshot = DesktopSnapshot.Empty;
    private IReadOnlyList<Platform> _platforms = [];
    private double _scale = 1.0;
    private double _lostSeconds;

    public PetSimulation(PetConfig config, DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(snapshot);
        _config = config;
        UpdateWorld(snapshot);

        var monitors = snapshot.Monitors;
        var start = monitors.FirstOrDefault(m => m.IsPrimary) ?? (monitors.Count > 0 ? monitors[0] : null);
        if (start is not null)
        {
            SpawnAtTop(start);
        }
        else
        {
            _body.Feet = new Vec2(100, 100);
        }

        Current = BuildFrame();
        Previous = Current;
    }

    /// <summary>State after the latest step.</summary>
    public PetFrame Current { get; private set; }

    /// <summary>State before the latest step; renderers interpolate between this and <see cref="Current"/>.</summary>
    public PetFrame Previous { get; private set; }

    public BodyMode Mode => _body.Mode;

    /// <summary>Number of times the safety net has respawned the pet (diagnostics/tests).</summary>
    public int RespawnCount { get; private set; }

    public void Tick(double dt, DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Previous = Current;
        UpdateWorld(snapshot);

        var teleported = false;
        var physicsEvent = PhysicsEvent.None;

        // No monitors (e.g. mid display change): freeze physics rather than fall forever.
        if (_snapshot.Monitors.Count > 0)
        {
            physicsEvent = PhysicsSystem.Step(_body, _platforms, dt, _scale, _config);
            teleported = ApplySafetyNet(dt);
            _scale = (WorldQuery.MonitorAt(_snapshot.Monitors, _body.Feet)
                ?? WorldQuery.Nearest(_snapshot.Monitors, _body.Feet))?.Scale ?? _scale;
        }

        UpdateAnimation(physicsEvent);
        _animation.Update(dt);

        Current = BuildFrame();
        if (teleported)
        {
            Previous = Current; // don't interpolate a streak across the screen
        }
    }

    /// <summary>Puts the pet somewhere, airborne. Used by tests now and by drag/throw in M4.</summary>
    internal void Place(Vec2 feet, Vec2 velocity)
    {
        _body.Feet = feet;
        _body.Release(velocity);
        _lostSeconds = 0;
        Current = BuildFrame();
        Previous = Current;
    }

    private void UpdateWorld(DesktopSnapshot snapshot)
    {
        if (ReferenceEquals(snapshot, _snapshot))
        {
            return;
        }

        _snapshot = snapshot;
        _platforms = PlatformBuilder.Build(snapshot);
    }

    private bool ApplySafetyNet(double dt)
    {
        if (!WorldQuery.IsLost(_snapshot.Monitors, _body.Feet))
        {
            _lostSeconds = 0;
            return false;
        }

        _lostSeconds += dt;
        if (_lostSeconds < _config.LostGraceSeconds)
        {
            return false;
        }

        var monitor = WorldQuery.Nearest(_snapshot.Monitors, _body.Feet)!;
        SpawnAtTop(monitor);
        RespawnCount++;
        return true;
    }

    private void SpawnAtTop(MonitorInfo monitor)
    {
        var area = monitor.WorkArea;
        _scale = monitor.Scale;
        _body.Feet = new Vec2(area.Left + (area.Width / 2.0), area.Top + SizePx());
        _body.Release(Vec2.Zero);
        _lostSeconds = 0;
    }

    private void UpdateAnimation(PhysicsEvent physicsEvent)
    {
        if (_body.Mode == BodyMode.Airborne)
        {
            _animation.Play(Animations.Fall);
        }
        else if (physicsEvent == PhysicsEvent.Landed)
        {
            _animation.Play(Animations.Land, restart: true);
        }
        else if (_animation.Current != Animations.Land || _animation.IsFinished)
        {
            _animation.Play(Animations.Idle);
        }
    }

    private double SizePx() => _config.SizeDip * _scale;

    private PetFrame BuildFrame()
    {
        var size = SizePx();
        return new PetFrame(_body.Feet, new Vec2(size, size), FacingLeft: false, _animation.Current.Id, _animation.FrameIndex);
    }
}

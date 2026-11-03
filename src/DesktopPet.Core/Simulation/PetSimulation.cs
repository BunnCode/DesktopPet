using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Simulation;

/// <summary>
/// The whole pet, stepped at a fixed rate against the latest desktop snapshot.
/// Each tick: behavior acts → physics resolves → physics events force Fall/Land → safety net → animation.
/// </summary>
public sealed class PetSimulation
{
    private readonly PetContext _ctx;
    private readonly StateMachine _fsm;

    private DesktopSnapshot _snapshot = DesktopSnapshot.Empty;
    private IReadOnlyList<Platform> _platforms = [];
    private double _lostSeconds;

    /// <param name="config">Tuning.</param>
    /// <param name="snapshot">Initial desktop; the pet spawns at the top of its primary monitor.</param>
    /// <param name="random">Randomness for behavior choices; seeded from the clock if omitted.</param>
    public PetSimulation(PetConfig config, DesktopSnapshot snapshot, IRandom? random = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(snapshot);
        _ctx = new PetContext(config, random ?? new SeededRandom(Environment.TickCount));
        UpdateWorld(snapshot);

        var monitors = snapshot.Monitors;
        var start = monitors.FirstOrDefault(m => m.IsPrimary) ?? (monitors.Count > 0 ? monitors[0] : null);
        if (start is not null)
        {
            SpawnAtTop(start);
        }
        else
        {
            _ctx.Body.Feet = new Vec2(100, 100);
        }

        _fsm = new StateMachine(_ctx, PetStateId.Fall);
        Current = BuildFrame();
        Previous = Current;
    }

    /// <summary>State after the latest step.</summary>
    public PetFrame Current { get; private set; }

    /// <summary>State before the latest step; renderers interpolate between this and <see cref="Current"/>.</summary>
    public PetFrame Previous { get; private set; }

    public BodyMode Mode => _ctx.Body.Mode;

    public PetStateId State => _fsm.Current.Id;

    /// <summary>Number of times the safety net has respawned the pet (diagnostics/tests).</summary>
    public int RespawnCount { get; private set; }

    public void Tick(double dt, DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Previous = Current;
        UpdateWorld(snapshot);

        var teleported = false;

        // No monitors (e.g. mid display change): freeze rather than fall forever.
        if (_snapshot.Monitors.Count > 0)
        {
            _fsm.Update(_ctx, dt);

            switch (PhysicsSystem.Step(_ctx.Body, _platforms, dt, _ctx.Scale, _ctx.Config))
            {
                case PhysicsEvent.Landed:
                    _fsm.ChangeTo(PetStateId.Land, _ctx);
                    break;
                case PhysicsEvent.LostGround:
                    _fsm.ChangeTo(PetStateId.Fall, _ctx);
                    break;
            }

            if (ApplySafetyNet(dt))
            {
                teleported = true;
                _fsm.ChangeTo(PetStateId.Fall, _ctx);
            }

            _ctx.Scale = (WorldQuery.MonitorAt(_snapshot.Monitors, _ctx.Body.Feet)
                ?? WorldQuery.Nearest(_snapshot.Monitors, _ctx.Body.Feet))?.Scale ?? _ctx.Scale;
        }

        _ctx.Animation.Update(dt);

        Current = BuildFrame();
        if (teleported)
        {
            Previous = Current; // don't interpolate a streak across the screen
        }
    }

    /// <summary>Puts the pet somewhere, airborne. Used by tests now and by drag/throw in M4.</summary>
    internal void Place(Vec2 feet, Vec2 velocity)
    {
        _ctx.Body.Feet = feet;
        _ctx.Body.Release(velocity);
        _lostSeconds = 0;
        _fsm.ChangeTo(PetStateId.Fall, _ctx);
        Current = BuildFrame();
        Previous = Current;
    }

    /// <summary>Test hook: switch behavior directly.</summary>
    internal void ForceState(PetStateId state, bool? facingLeft = null)
    {
        if (facingLeft is { } f)
        {
            _ctx.FacingLeft = f;
        }

        _fsm.ChangeTo(state, _ctx);
    }

    private void UpdateWorld(DesktopSnapshot snapshot)
    {
        if (ReferenceEquals(snapshot, _snapshot))
        {
            return;
        }

        _snapshot = snapshot;
        _platforms = PlatformBuilder.Build(snapshot);
        _ctx.Monitors = snapshot.Monitors;
    }

    private bool ApplySafetyNet(double dt)
    {
        if (!WorldQuery.IsLost(_snapshot.Monitors, _ctx.Body.Feet))
        {
            _lostSeconds = 0;
            return false;
        }

        _lostSeconds += dt;
        if (_lostSeconds < _ctx.Config.LostGraceSeconds)
        {
            return false;
        }

        SpawnAtTop(WorldQuery.Nearest(_snapshot.Monitors, _ctx.Body.Feet)!);
        RespawnCount++;
        return true;
    }

    private void SpawnAtTop(MonitorInfo monitor)
    {
        var area = monitor.WorkArea;
        _ctx.Scale = monitor.Scale;
        _ctx.Body.Feet = new Vec2(area.Left + (area.Width / 2.0), area.Top + _ctx.SizePx);
        _ctx.Body.Release(Vec2.Zero);
        _lostSeconds = 0;
    }

    private PetFrame BuildFrame()
    {
        var size = _ctx.SizePx;
        return new PetFrame(
            _ctx.Body.Feet, new Vec2(size, size), _ctx.FacingLeft,
            _ctx.Animation.Current.Id, _ctx.Animation.FrameIndex, _fsm.Current.Id);
    }
}

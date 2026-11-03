using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Input;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Stats;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Simulation;

/// <summary>
/// The whole pet, stepped at a fixed rate against the latest desktop snapshot.
/// Each tick: behavior acts → physics resolves → physics events force Fall/Land → safety net → animation.
/// Pointer input arrives between ticks via <see cref="PointerDown"/>/<see cref="PointerMove"/>/<see cref="PointerUp"/>.
/// </summary>
public sealed class PetSimulation
{
    private readonly PetContext _ctx;
    private readonly StateMachine _fsm;
    private readonly ThrowEstimator _throw;
    private readonly IClock _clock;

    private DesktopSnapshot _snapshot = DesktopSnapshot.Empty;
    private IReadOnlyList<Platform> _platforms = [];
    private double _lostSeconds;
    private DateTimeOffset _statsUpdatedAt;

    // Pointer gesture in progress.
    private Vec2? _pressAt;
    private Vec2 _grabOffset;

    /// <param name="config">Tuning.</param>
    /// <param name="snapshot">Initial desktop; the pet spawns at the top of its primary monitor.</param>
    /// <param name="random">Randomness for behavior choices; seeded from the clock if omitted.</param>
    /// <param name="clock">Wall clock for stat decay; the system clock if omitted.</param>
    /// <param name="save">
    /// A previous run's save. Its stats are caught up for the time since it was written, counted as time away
    /// (so nothing drops to critical while the app was closed). A fresh pet if omitted.
    /// </param>
    public PetSimulation(PetConfig config, DesktopSnapshot snapshot, IRandom? random = null, IClock? clock = null, SaveData? save = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(snapshot);
        _ctx = new PetContext(config, random ?? new SeededRandom(Environment.TickCount));
        _throw = new ThrowEstimator(config.ThrowWindowSeconds);
        _clock = clock ?? SystemClock.Instance;
        _statsUpdatedAt = _clock.UtcNow;
        if (save is not null)
        {
            var offline = _statsUpdatedAt - save.SavedAtUtc;
            _ctx.Stats = StatsSystem.CatchUp(save.Stats, offline, config.Stats);
        }

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

        _fsm = new StateMachine(_ctx, _ctx.Body.Mode == BodyMode.Hanging ? PetStateId.Hang : PetStateId.Fall);
        Current = BuildFrame();
        Previous = Current;
    }

    /// <summary>State after the latest step.</summary>
    public PetFrame Current { get; private set; }

    /// <summary>State before the latest step; renderers interpolate between this and <see cref="Current"/>.</summary>
    public PetFrame Previous { get; private set; }

    public BodyMode Mode => _ctx.Body.Mode;

    public PetStateId State => _fsm.Current.Id;

    public Vec2 Velocity => _ctx.Body.Velocity;

    /// <summary>Surfaces derived from the latest snapshot (for the debug overlay).</summary>
    public IReadOnlyList<Platform> Platforms => _platforms;

    /// <summary>Key of the surface the pet stands on or hangs from, or null when airborne or held.</summary>
    public string? GroundKey => _ctx.Body.Mode is BodyMode.Grounded or BodyMode.Hanging ? _ctx.Body.GroundKey : null;

    /// <summary>True while the pet is being dragged; the host should feed <see cref="PointerMove"/> every frame.</summary>
    public bool IsHeld => _ctx.Body.Mode == BodyMode.Held;

    /// <summary>Number of times the safety net has respawned the pet (diagnostics/tests).</summary>
    public int RespawnCount { get; private set; }

    public PetStats Stats => _ctx.Stats;

    /// <summary>
    /// Set by the host while the user is away (screen locked). Stats keep decaying but can't fall to critical.
    /// </summary>
    public bool UserAway { get; set; }

    /// <summary>A snapshot of what should survive a restart.</summary>
    public SaveData CreateSave() => SaveData.From(_ctx.Stats, _clock.UtcNow);

    /// <summary>
    /// Menu actions. Stats change immediately; the matching behavior plays if the pet is on the ground
    /// (a held, falling or hanging pet still gets fed, it just doesn't stop to munch).
    /// </summary>
    public void Apply(PetAction action)
    {
        UpdateStats();
        _ctx.Stats = StatsSystem.Apply(_ctx.Stats, action, _ctx.Config.Stats);
        var grounded = _ctx.Body.Mode == BodyMode.Grounded;

        switch (action)
        {
            case PetAction.Feed when grounded:
                _fsm.ChangeTo(PetStateId.Eat, _ctx);
                break;
            case PetAction.Play when grounded:
                _fsm.ChangeTo(PetStateId.Play, _ctx);
                break;
            case PetAction.Sleep:
                _ctx.TuckedIn = true;
                if (grounded)
                {
                    _fsm.ChangeTo(PetStateId.Sleep, _ctx);
                }

                break;
            case PetAction.WakeUp when State == PetStateId.Sleep:
                _ctx.TuckedIn = false;
                _fsm.ChangeTo(PetStateId.Idle, _ctx);
                break;
        }
    }

    public void Tick(double dt, DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Previous = Current;
        UpdateWorld(snapshot);
        UpdateStats();

        var teleported = false;

        // No monitors (e.g. mid display change): freeze rather than fall forever.
        if (_snapshot.Monitors.Count > 0)
        {
            _fsm.Update(_ctx, dt);

            var world = new PhysicsWorld(_platforms, _snapshot.Monitors, _ctx.Scale);
            switch (PhysicsSystem.Step(_ctx.Body, world, dt, _ctx.Config))
            {
                case PhysicsEvent.Landed when State != PetStateId.Play: // play hops land without the squash
                    _fsm.ChangeTo(PetStateId.Land, _ctx);
                    break;
                case PhysicsEvent.LostGround:
                    _fsm.ChangeTo(PetStateId.Fall, _ctx);
                    break;
                case PhysicsEvent.Grabbed:
                    _fsm.ChangeTo(PetStateId.Hang, _ctx);
                    break;
                case PhysicsEvent.HitWall when _ctx.Body.Velocity.X != 0:
                    _ctx.FacingLeft = _ctx.Body.Velocity.X < 0; // face the way it's bouncing
                    break;
            }

            if (!IsHeld && ApplySafetyNet(dt))
            {
                teleported = true;
                _fsm.ChangeTo(_ctx.Body.Mode == BodyMode.Hanging ? PetStateId.Hang : PetStateId.Fall, _ctx);
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

    // ---- Pointer input (physical screen pixels, timestamps in seconds) ----

    /// <summary>Left button pressed on the pet. The platform layer only reports presses on visible pixels.</summary>
    public void PointerDown(Vec2 position, double time)
    {
        _pressAt = position;
        _throw.Clear();
        _throw.Add(time, position);
    }

    public void PointerMove(Vec2 position, double time)
    {
        if (_pressAt is not { } pressAt)
        {
            return;
        }

        if (!IsHeld)
        {
            var threshold = _ctx.Config.DragThresholdDip * _ctx.Scale;
            if ((position - pressAt).LengthSquared < threshold * threshold)
            {
                return;
            }

            // Pick up, keeping the pet where it was relative to the cursor at the press (no jump).
            _grabOffset = _ctx.Body.Feet - pressAt;
            _ctx.Body.Grab();
            _lostSeconds = 0;
            _fsm.ChangeTo(PetStateId.Held, _ctx);
        }

        _ctx.Body.Feet = position + _grabOffset;
        _throw.Add(time, position);
    }

    public void PointerUp(Vec2 position, double time)
    {
        if (_pressAt is null)
        {
            return;
        }

        _pressAt = null;

        if (IsHeld)
        {
            _ctx.Body.Feet = position + _grabOffset;
            _throw.Add(time, position);
            var velocity = ClampSpeed(_throw.Velocity(time), _ctx.Config.MaxThrowSpeedDip * _ctx.Scale);

            // Let go with its hands at a title bar: it grabs on (unless flung hard).
            var reach = _ctx.Config.HangReachDip * _ctx.Scale;
            if (velocity.Length <= _ctx.Config.HangCatchMaxSpeedDip * _ctx.Scale
                && PhysicsSystem.FindBarInReach(_platforms, _ctx.Body.Feet, _ctx.SizePx, reach) is { } bar)
            {
                _ctx.Body.Hang(_ctx.Body.Feet.X, bar, _ctx.SizePx);
                _fsm.ChangeTo(PetStateId.Hang, _ctx);
                return;
            }

            _ctx.Body.Release(velocity);
            if (velocity.X != 0)
            {
                _ctx.FacingLeft = velocity.X < 0;
            }

            _fsm.ChangeTo(PetStateId.Fall, _ctx);
        }
        else if (State == PetStateId.Sleep)
        {
            Apply(PetAction.WakeUp); // a click wakes it
        }
        else if (_ctx.Body.Mode == BodyMode.Grounded)
        {
            UpdateStats();
            _ctx.Stats = StatsSystem.Petted(_ctx.Stats, _ctx.Config.Stats);
            _fsm.ChangeTo(PetStateId.Happy, _ctx); // a click: being petted
        }
    }

    /// <summary>Abandons a gesture (e.g. mouse capture lost); a held pet is dropped in place.</summary>
    public void PointerCancel()
    {
        if (IsHeld)
        {
            _ctx.Body.Release(Vec2.Zero);
            _fsm.ChangeTo(PetStateId.Fall, _ctx);
        }

        _pressAt = null;
    }

    /// <summary>Puts the pet somewhere, airborne. Used by tests.</summary>
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

    /// <summary>
    /// Advances stats by the real time since the last update. A long gap means the process was frozen
    /// (PC asleep/hibernating) and counts as time away, like a locked screen.
    /// </summary>
    private void UpdateStats()
    {
        var now = _clock.UtcNow;
        var elapsed = now - _statsUpdatedAt;
        _statsUpdatedAt = now;
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        var rules = _ctx.Config.Stats;
        var away = UserAway || elapsed > rules.AwayGap;
        var activity = State switch
        {
            PetStateId.Sleep => StatActivity.Sleeping,
            PetStateId.Walk or PetStateId.Play or PetStateId.Shimmy => StatActivity.Active,
            _ => StatActivity.Resting,
        };

        // Frozen-process gaps: the pet "napped" through them, like offline time.
        if (elapsed > rules.AwayGap)
        {
            activity = StatActivity.Sleeping;
        }

        _ctx.Stats = StatsSystem.Advance(_ctx.Stats, elapsed, activity, away, rules);
    }

    private static Vec2 ClampSpeed(Vec2 v, double max)
    {
        var length = v.Length;
        return length > max ? v * (max / length) : v;
    }

    private void UpdateWorld(DesktopSnapshot snapshot)
    {
        if (ReferenceEquals(snapshot, _snapshot))
        {
            return;
        }

        _snapshot = snapshot;
        _platforms = PlatformBuilder.Build(snapshot, _ctx.Config.CaptionHeightDip, includeHangBars: _ctx.Config.EnableHanging);
        _ctx.Monitors = snapshot.Monitors;
        _ctx.Platforms = _platforms;
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

    /// <summary>
    /// Appears at the top-centre of the monitor and falls, or, if a maximized window's title bar is right there,
    /// starts out hanging from it.
    /// </summary>
    private void SpawnAtTop(MonitorInfo monitor)
    {
        var area = monitor.WorkArea;
        _ctx.Scale = monitor.Scale;
        _ctx.Body.Feet = new Vec2(area.Left + (area.Width / 2.0), area.Top + _ctx.SizePx);
        _lostSeconds = 0;

        var bar = _platforms
            .Where(p => p.Kind == PlatformKind.HangBar && p.SpansX(_ctx.Body.Feet.X) && p.Y >= area.Top && p.Y < area.Top + _ctx.SizePx)
            .OrderBy(p => p.Y)
            .Cast<Platform?>()
            .FirstOrDefault();

        if (bar is { } b)
        {
            _ctx.Body.Hang(_ctx.Body.Feet.X, b, _ctx.SizePx);
        }
        else
        {
            _ctx.Body.Release(Vec2.Zero);
        }
    }

    private PetFrame BuildFrame()
    {
        var size = _ctx.SizePx;
        return new PetFrame(
            _ctx.Body.Feet, new Vec2(size, size), _ctx.FacingLeft,
            _ctx.Animation.Current.Id, _ctx.Animation.FrameIndex, _fsm.Current.Id);
    }
}

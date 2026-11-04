using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Input;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Traits;
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
    private ThrowEstimator _throw;
    private readonly IClock _clock;
    private PetConfig _sharedConfig;

    private DesktopSnapshot _snapshot = DesktopSnapshot.Empty;
    private IReadOnlyList<Platform> _platforms = [];
    private double _lostSeconds;
    private DateTimeOffset _statsUpdatedAt;

    // Pointer gesture in progress.
    private Vec2? _pressAt;
    private Vec2 _grabOffset;

    /// <param name="config">Shared tuning; the pet's <paramref name="traits"/> are applied on top.</param>
    /// <param name="snapshot">Initial desktop; the pet spawns at the top of its primary monitor.</param>
    /// <param name="random">Randomness for behavior choices; seeded from the clock if omitted.</param>
    /// <param name="clock">Wall clock for stat decay; the system clock if omitted.</param>
    /// <param name="save">
    /// A previous run's save. Its stats are caught up for the time since it was written, counted as time away
    /// (so nothing drops to critical while the app was closed). A fresh pet if omitted.
    /// </param>
    /// <param name="traits">This pet's name and multipliers; an average pet if omitted.</param>
    /// <param name="furniture">The furniture, shared by all pets; none if omitted.</param>
    public PetSimulation(
        PetConfig config, DesktopSnapshot snapshot, IRandom? random = null, IClock? clock = null, SaveData? save = null,
        PetTraits? traits = null, FurnitureWorld? furniture = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(snapshot);
        _sharedConfig = config;
        Traits = traits ?? PetTraits.Neutral("Pet");
        config = Traits.ApplyTo(config);
        _ctx = new PetContext(config, random ?? new SeededRandom(Environment.TickCount), furniture ?? new FurnitureWorld(config), this);
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

    /// <summary>Key of the surface the pet stands on, hangs from or clings to, or null when airborne or held.</summary>
    public string? GroundKey =>
        _ctx.Body.Mode is BodyMode.Grounded or BodyMode.Hanging or BodyMode.Clinging ? _ctx.Body.GroundKey : null;

    /// <summary>The settings this pet runs on: the shared ones with its traits applied.</summary>
    public PetConfig Config => _ctx.Config;

    /// <summary>This pet's name and multipliers.</summary>
    public PetTraits Traits { get; private set; }

    /// <summary>Swaps in edited traits live (the pet's traits.json changed). Stats and behavior carry on.</summary>
    public void SetTraits(PetTraits traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        UpdateStats(); // time so far decays at the old rates
        Traits = traits;
        ApplyConfig(traits.ApplyTo(_sharedConfig));
    }

    /// <summary>
    /// Swaps in new shared tuning without restarting: the pet keeps its position, stats and current behavior; surfaces and walls
    /// are rebuilt on the next tick (so switching off climbing or hanging drops a pet that relied on them).
    /// The menu's push switch follows the new config only if the config's own push setting changed.
    /// The fixed step rate is the host's business and isn't changed here.
    /// </summary>
    public void Reconfigure(PetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        UpdateStats();
        _sharedConfig = config;
        ApplyConfig(Traits.ApplyTo(config));
    }

    private void ApplyConfig(PetConfig config)
    {
        var old = _ctx.Config;
        _ctx.Config = config;
        _throw = new ThrowEstimator(config.ThrowWindowSeconds);
        if (config.EnablePushing != old.EnablePushing)
        {
            _ctx.PushingEnabled = config.EnablePushing;
        }

        _snapshot = DesktopSnapshot.Empty; // force UpdateWorld to rebuild platforms and walls with the new switches
    }

    /// <summary>The furniture the pet is heading for or using, if any.</summary>
    public FurnitureItem? FurnitureTarget => _ctx.Errand?.Item;

    /// <summary>The pen the pet is shut in, if any.</summary>
    public FurnitureItem? Pen => _ctx.Pen;

    /// <summary>Frees any furniture slot the pet holds (it's being removed).</summary>
    public void LeaveFurniture()
    {
        _ctx.CancelErrand();
        _ctx.Pen = null;
    }

    /// <summary>Whether the pet may push windows (it moves the user's real windows). Starts from config.</summary>
    public bool PushingEnabled
    {
        get => _ctx.PushingEnabled;
        set => _ctx.PushingEnabled = value;
    }

    /// <summary>Climbable walls derived from the latest snapshot (for the debug overlay).</summary>
    public IReadOnlyList<Wall> Walls => _ctx.Walls;

    /// <summary>Window moves requested since the last call (pushing). The host carries them out.</summary>
    public IReadOnlyList<WindowCommand> TakeCommands()
    {
        if (_ctx.Commands.Count == 0)
        {
            return [];
        }

        var commands = _ctx.Commands.ToArray();
        _ctx.Commands.Clear();
        return commands;
    }

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
        if (action != PetAction.WakeUp || State == PetStateId.UseFurniture)
        {
            _ctx.CancelErrand(); // the user's care comes first
        }

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
            case PetAction.WakeUp when State is PetStateId.Sleep or PetStateId.UseFurniture:
                _ctx.TuckedIn = false;
                _fsm.ChangeTo(PetStateId.Idle, _ctx);
                break;
        }
    }

    public void Tick(double dt, DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Previous = Current;
        _ctx.Time += dt;
        UpdateWorld(snapshot);
        UpdateStats();
        CheckPen();

        var teleported = false;

        // No monitors (e.g. mid display change): freeze rather than fall forever.
        if (_snapshot.Monitors.Count > 0)
        {
            _fsm.Update(_ctx, dt);

            var world = new PhysicsWorld(_platforms, _snapshot.Monitors, _ctx.Scale, _ctx.Walls);
            switch (PhysicsSystem.Step(_ctx.Body, world, dt, _ctx.Config))
            {
                case PhysicsEvent.Landed:
                    OnLanded();
                    break;
                case PhysicsEvent.LostGround:
                    _fsm.ChangeTo(PetStateId.Fall, _ctx);
                    break;
                case PhysicsEvent.Grabbed:
                    _fsm.ChangeTo(PetStateId.Hang, _ctx);
                    break;
                case PhysicsEvent.Clung:
                    _fsm.ChangeTo(PetStateId.Cling, _ctx);
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
            _ctx.CancelErrand();
            _ctx.Pen = null; // taken out of its pen
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

            // Let go with its side against a wall: it grabs on (unless flung hard).
            if (_ctx.Config.EnableClimbing && velocity.Length <= _ctx.Config.ClingMaxSpeedDip * _ctx.Scale
                && WallInReach(_ctx.Config.ClingReachDip * _ctx.Scale) is { } wall)
            {
                _ctx.Body.Cling(wall, _ctx.Body.Feet.Y, _ctx.SizePx);
                _fsm.ChangeTo(PetStateId.Cling, _ctx);
                return;
            }

            _ctx.Body.Release(velocity);
            _ctx.UserDropped = true; // landing in a pen shuts it in
            if (velocity.X != 0)
            {
                _ctx.FacingLeft = velocity.X < 0;
            }

            _fsm.ChangeTo(PetStateId.Fall, _ctx);
        }
        else if (State == PetStateId.Sleep || (State == PetStateId.UseFurniture && _ctx.ActiveUse?.Animation == Animation.Animations.Sleep.Id))
        {
            Apply(PetAction.WakeUp); // a click wakes it (also asleep on furniture)
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

    /// <summary>Drops the pet in from <paramref name="feet"/> (e.g. a newly added pet arriving from the top of the screen).</summary>
    public void DropFrom(Vec2 feet) => Place(feet, Vec2.Zero);

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
        var use = State == PetStateId.UseFurniture ? _ctx.ActiveUse : null;
        var activity = State switch
        {
            PetStateId.Sleep => StatActivity.Sleeping,
            PetStateId.Walk or PetStateId.Play or PetStateId.Shimmy or PetStateId.Climb or PetStateId.Push
                or PetStateId.GoToFurniture => StatActivity.Active,
            PetStateId.UseFurniture when use is not null => UseActivity(use),
            _ => StatActivity.Resting,
        };

        // Frozen-process gaps: the pet "napped" through them, like offline time.
        if (elapsed > rules.AwayGap)
        {
            activity = StatActivity.Sleeping;
        }

        var before = _ctx.Stats;
        var after = StatsSystem.Advance(before, elapsed, activity, away, rules);

        // Furniture meets its need while it's used (not across a frozen-process gap: the pet wasn't using it then).
        if (use is { Need: not Need.None } && elapsed <= rules.AwayGap)
        {
            var gain = use.RatePerHour * elapsed.TotalHours;
            after = (use.Need switch
            {
                Need.Hunger => after with { Hunger = after.Hunger + gain },
                Need.Happiness => after with { Happiness = after.Happiness + gain },
                _ => after with { Energy = after.Energy + gain },
            }).Clamped();
        }

        // Shut in a pen: needs don't drop.
        if (_ctx.Pen is not null)
        {
            after = new PetStats(
                System.Math.Max(before.Hunger, after.Hunger),
                System.Math.Max(before.Happiness, after.Happiness),
                System.Math.Max(before.Energy, after.Energy));
        }

        _ctx.Stats = after;
    }

    private static StatActivity UseActivity(FurnitureUse use) =>
        use.Animation == Animation.Animations.Sleep.Id ? StatActivity.Sleeping
        : use.Effect is UseEffect.Kick or UseEffect.Hop or UseEffect.Bounce ? StatActivity.Active
        : StatActivity.Resting;

    /// <summary>
    /// Touched down. A squash (Land) unless the hop was part of playing or using furniture. Dropped by the user into a
    /// pen, it's shut in.
    /// </summary>
    private void OnLanded()
    {
        var dropped = _ctx.UserDropped;
        _ctx.UserDropped = false;
        if (dropped && _ctx.Furniture.PenAt(_ctx.Body.GroundKey, _ctx.Body.Feet.X) is { } pen)
        {
            _ctx.CancelErrand();
            _ctx.Pen = pen;
            var margin = _ctx.HalfWidthPx + (2 * _ctx.Scale);
            var x = pen.Right - pen.Left > 2 * margin
                ? System.Math.Clamp(_ctx.Body.Feet.X, pen.Left + margin, pen.Right - margin)
                : pen.Feet.X;
            _ctx.Body.Feet = _ctx.Body.Feet with { X = x };
            _ctx.Body.Velocity = Vec2.Zero; // no skidding out of it
        }

        if (State is not (PetStateId.Play or PetStateId.UseFurniture))
        {
            _fsm.ChangeTo(PetStateId.Land, _ctx);
        }
    }

    /// <summary>
    /// The pen keeps the pet only while it stands where the pet is: picked up, removed or gone from under it, the
    /// pet is let out.
    /// </summary>
    private void CheckPen()
    {
        if (_ctx.Pen is not { } pen)
        {
            return;
        }

        var body = _ctx.Body;
        var gone = pen.IsRemoved || pen.IsHeld
            || (body.Mode == BodyMode.Grounded
                && (pen.GroundKey != body.GroundKey || body.Feet.X < pen.Left - _ctx.HalfWidthPx || body.Feet.X > pen.Right + _ctx.HalfWidthPx));
        if (gone)
        {
            _ctx.Pen = null;
        }
    }

    /// <summary>The nearest wall the pet faces with its side within <paramref name="reach"/>, alongside its middle.</summary>
    private Wall? WallInReach(double reach)
    {
        var feet = _ctx.Body.Feet;
        var midY = feet.Y - (_ctx.SizePx / 2);
        Wall? best = null;
        var bestDistance = double.MaxValue;
        foreach (var w in _ctx.Walls)
        {
            if (!w.SpansY(midY))
            {
                continue;
            }

            var side = feet.X + (w.TowardWall * _ctx.HalfWidthPx);
            var distance = System.Math.Abs(w.X - side);

            // Must be on the wall's outer side (not reaching through the window from inside).
            var onCorrectSide = w.TowardWall > 0 ? feet.X < w.X : feet.X > w.X;
            if (onCorrectSide && distance <= reach && distance < bestDistance)
            {
                best = w;
                bestDistance = distance;
            }
        }

        return best;
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
        // Built whenever either feature could use them (pushing can be switched back on at runtime).
        _ctx.Walls = _ctx.Config.EnableClimbing || _ctx.Config.EnablePushing ? WallBuilder.Build(snapshot) : [];
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
        var sink = _ctx.Animation.Current == Animation.Animations.SitLedge ? size * Animation.Animations.LedgeSinkFraction : 0;
        if (State == PetStateId.UseFurniture && _ctx.ActiveUse is { SeatDip: > 0 } use)
        {
            sink = -use.SeatDip * _ctx.Scale; // up on the bed or couch
        }

        return new PetFrame(
            _ctx.Body.Feet, new Vec2(size, size), _ctx.FacingLeft,
            _ctx.Animation.Current.Id, _ctx.Animation.FrameIndex, _fsm.Current.Id, sink);
    }
}

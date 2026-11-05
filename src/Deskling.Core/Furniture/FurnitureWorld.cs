using Deskling.Core.Breeding;
using Deskling.Core.Death;
using Deskling.Core.Input;
using Deskling.Core.Math;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;
using Deskling.Core.World;

namespace Deskling.Core.Furniture;

/// <summary>
/// All the furniture, shared by every pet. Steps the furniture's physics (it falls, lands on floors and window tops,
/// rides moving windows, and a ball bounces and rolls), handles dragging it with the mouse, and keeps track of which
/// pet holds which slot. Pets read it and kick balls during their own ticks; the host ticks it once per step.
/// </summary>
public sealed class FurnitureWorld
{
    /// <summary>Landing slower than this (DIP/s) doesn't bounce, so a ball comes to rest.</summary>
    private const double MinBounceSpeedDip = 150;

    private readonly List<FurnitureItem> _items = [];
    private readonly Dictionary<string, FurnitureSpec> _specs = new(StringComparer.OrdinalIgnoreCase);
    private PetConfig _config;
    private DesktopSnapshot _snapshot = DesktopSnapshot.Empty;
    private IReadOnlyList<Platform> _platforms = [];
    private int _nextId = 1;

    // Pointer gesture in progress.
    private FurnitureItem? _pressed;
    private Vec2 _pressAt;
    private Vec2 _grabOffset;
    private ThrowEstimator _throw;

    public FurnitureWorld(PetConfig config, IEnumerable<FurnitureSpec>? specs = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _throw = new ThrowEstimator(config.ThrowWindowSeconds);
        if (specs is not null)
        {
            SetSpecs(specs);
        }
    }

    /// <summary>The furniture in the world, oldest first.</summary>
    public IReadOnlyList<FurnitureItem> Items => _items;

    /// <summary>The kinds that can be placed, in menu order (not the egg or the gravestone).</summary>
    public IReadOnlyList<FurnitureSpec> Specs => [.. _specs.Values.Where(s => s.Kind != Eggs.Kind && s.Kind != Graves.Kind)];

    /// <summary>The eggs in the world, oldest first.</summary>
    public IEnumerable<FurnitureItem> EggItems => _items.Where(i => i.Egg is not null);

    /// <summary>The gravestones in the world, oldest first.</summary>
    public IEnumerable<FurnitureItem> GraveItems => _items.Where(i => i.Grave is not null);

    /// <summary>Goes up whenever furniture is added, removed or put down somewhere new, so the host knows to save.</summary>
    public int Version { get; private set; }

    /// <summary>A piece of furniture is being dragged; the host should feed <see cref="PointerMove"/> every frame.</summary>
    public bool IsDragging => _pressed is not null;

    /// <summary>Swaps in new shared tuning (gravity, friction, drag threshold).</summary>
    public void Reconfigure(PetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _throw = new ThrowEstimator(config.ThrowWindowSeconds);
        foreach (var item in _items)
        {
            item.PhysicsConfig = null;
        }
    }

    /// <summary>
    /// The current definitions (live reload). Furniture of a kind that's still defined takes the new definition;
    /// furniture of a kind that's gone is removed. The egg and the gravestone are built in and always stay.
    /// </summary>
    public void SetSpecs(IEnumerable<FurnitureSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        _specs.Clear();
        foreach (var spec in specs)
        {
            _specs[spec.Kind] = spec;
        }

        _specs[Eggs.Kind] = Eggs.Spec;
        _specs[Graves.Kind] = Graves.Spec;

        foreach (var item in _items.ToList())
        {
            if (_specs.TryGetValue(item.Kind, out var spec))
            {
                if (spec != item.Spec)
                {
                    item.SetSpec(spec);
                }
            }
            else
            {
                Remove(item);
            }
        }
    }

    /// <summary>Drops a new piece of furniture in at <paramref name="feet"/>. Null if the kind isn't defined.</summary>
    public FurnitureItem? Add(string kind, Vec2 feet)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (!_specs.TryGetValue(kind, out var spec))
        {
            return null;
        }

        var item = new FurnitureItem(_nextId++, spec);
        item.Scale = Scale(feet);
        item.Body.Feet = feet;
        item.Body.Release(Vec2.Zero);
        item.UpdateFrame(snap: true);
        _items.Add(item);
        Version++;
        return item;
    }

    /// <summary>Lays an egg at <paramref name="feet"/>; it drops onto whatever is below.</summary>
    public FurnitureItem AddEgg(Vec2 feet, EggInfo egg)
    {
        ArgumentNullException.ThrowIfNull(egg);
        _specs[Eggs.Kind] = Eggs.Spec;
        var item = Add(Eggs.Kind, feet)!;
        item.Egg = egg;
        return item;
    }

    /// <summary>Puts a gravestone at <paramref name="feet"/>; it drops onto whatever is below.</summary>
    public FurnitureItem AddGrave(Vec2 feet, GraveInfo grave)
    {
        ArgumentNullException.ThrowIfNull(grave);
        _specs[Graves.Kind] = Graves.Spec;
        var item = Add(Graves.Kind, feet)!;
        item.Grave = grave;
        return item;
    }

    /// <summary>Updates whose gravestone it is (its pet's details were reread).</summary>
    public void SetGrave(FurnitureItem item, GraveInfo grave)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(grave);
        if (item.Grave is not null && item.Grave != grave)
        {
            item.Grave = grave;
            Version++;
        }
    }

    public void Remove(FurnitureItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_items.Remove(item))
        {
            return;
        }

        item.IsRemoved = true;
        if (_pressed == item)
        {
            _pressed = null;
        }

        Version++;
    }

    public FurnitureItem? Find(int id) => _items.FirstOrDefault(i => i.Id == id);

    public void Tick(double dt, DesktopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(snapshot, _snapshot))
        {
            _snapshot = snapshot;
            _platforms = PlatformBuilder.Build(snapshot, _config.CaptionHeightDip, includeHangBars: false);
        }

        foreach (var item in _items)
        {
            var snap = false;
            if (snapshot.Monitors.Count > 0 && !item.IsHeld)
            {
                snap = Step(item, dt);
            }

            item.Animate(dt);
            item.UpdateFrame(snap);
        }
    }

    // ---- Mouse ----

    /// <summary>Left button pressed on a piece of furniture.</summary>
    public void PointerDown(FurnitureItem item, Vec2 position, double time)
    {
        ArgumentNullException.ThrowIfNull(item);
        _pressed = item;
        _pressAt = position;
        _throw.Clear();
        _throw.Add(time, position);
    }

    public void PointerMove(Vec2 position, double time)
    {
        if (_pressed is not { } item)
        {
            return;
        }

        if (!item.IsHeld)
        {
            var threshold = _config.DragThresholdDip * item.Scale;
            if ((position - _pressAt).LengthSquared < threshold * threshold)
            {
                return;
            }

            _grabOffset = item.Body.Feet - _pressAt;
            item.Body.Grab();
            item.LostSeconds = 0;
        }

        item.Body.Feet = position + _grabOffset;
        item.Scale = Scale(item.Body.Feet);
        item.UpdateFrame(snap: true);
        _throw.Add(time, position);
    }

    public void PointerUp(Vec2 position, double time)
    {
        if (_pressed is not { } item)
        {
            return;
        }

        _pressed = null;
        if (item.IsHeld)
        {
            item.Body.Feet = position + _grabOffset;
            _throw.Add(time, position);
            var velocity = _throw.Velocity(time);
            var max = _config.MaxThrowSpeedDip * item.Scale;
            if (velocity.Length > max)
            {
                velocity *= max / velocity.Length;
            }

            item.Body.Release(velocity);
            Version++;
        }
        else if (item.Spec.Uses.Any(u => u.Effect == UseEffect.Kick))
        {
            // A click on a ball pokes it.
            var poke = _config.Furniture.KickLiftDip * item.Scale;
            item.Body.Release(new Vec2(0, -poke));
        }
    }

    /// <summary>Abandons a gesture; held furniture drops where it is.</summary>
    public void PointerCancel()
    {
        if (_pressed is { IsHeld: true } item)
        {
            item.Body.Release(Vec2.Zero);
            Version++;
        }

        _pressed = null;
    }

    // ---- For pets ----

    /// <summary>A pet kicks the item (a ball).</summary>
    internal static void Kick(FurnitureItem item, Vec2 velocity)
    {
        if (!item.IsHeld && !item.IsRemoved)
        {
            item.Body.Release(velocity);
        }
    }

    /// <summary>The pen standing on <paramref name="groundKey"/> whose span contains <paramref name="x"/>, if any.</summary>
    internal FurnitureItem? PenAt(string? groundKey, double x)
    {
        if (groundKey is null)
        {
            return null;
        }

        foreach (var item in _items)
        {
            if (item.Spec.IsPen && item.GroundKey == groundKey && x >= item.Left && x < item.Right)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// A pen on <paramref name="groundKey"/> whose side a pet's leading edge would cross going from
    /// <paramref name="fromEdge"/> to <paramref name="toEdge"/> (from outside).
    /// </summary>
    internal FurnitureItem? PenAhead(string? groundKey, double fromEdge, double toEdge)
    {
        if (groundKey is null)
        {
            return null;
        }

        foreach (var item in _items)
        {
            if (!item.Spec.IsPen || item.GroundKey != groundKey)
            {
                continue;
            }

            var side = toEdge > fromEdge ? item.Left : item.Right;
            var crosses = toEdge > fromEdge ? fromEdge <= side && toEdge > side : fromEdge >= side && toEdge < side;
            if (crosses)
            {
                return item;
            }
        }

        return null;
    }

    // ---- Saving ----

    public RoomSave CreateSave() =>
        new() { Items = [.. _items.Select(i => new RoomItem(i.Kind, System.Math.Round(i.Feet.X), System.Math.Round(i.Feet.Y), i.Egg, i.Grave))] };

    /// <summary>Puts saved furniture back (dropped from where it was, so it settles on whatever is there now).</summary>
    public void Load(RoomSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        foreach (var saved in save.Items ?? [])
        {
            if (saved?.Kind is not { } kind || !double.IsFinite(saved.X) || !double.IsFinite(saved.Y))
            {
                continue;
            }

            var feet = new Vec2(saved.X, saved.Y - 1);
            if (string.Equals(kind, Eggs.Kind, StringComparison.OrdinalIgnoreCase))
            {
                if (saved.Egg is { Child: not null, Parents: not null } egg)
                {
                    AddEgg(feet, egg);
                }
            }
            else if (string.Equals(kind, Graves.Kind, StringComparison.OrdinalIgnoreCase))
            {
                if (saved.Grave is { PetId: >= 1, Name: not null } grave)
                {
                    AddGrave(feet, grave);
                }
            }
            else
            {
                Add(kind, feet);
            }
        }

        Version = 0;
    }

    // ---- Physics ----

    /// <summary>One physics step. Returns true if it was teleported (respawned), so it isn't interpolated.</summary>
    private bool Step(FurnitureItem item, double dt)
    {
        var config = item.PhysicsConfig ??= PhysicsConfigFor(item.Spec);
        var fallSpeed = item.Body.Velocity.Y;
        var world = new PhysicsWorld(_platforms, _snapshot.Monitors, item.Scale);
        var result = PhysicsSystem.Step(item.Body, world, dt, config);

        if (result == PhysicsEvent.Landed && item.Spec.Bounciness > 0 && fallSpeed > MinBounceSpeedDip * item.Scale)
        {
            item.Body.Release(new Vec2(item.Body.Velocity.X, -fallSpeed * item.Spec.Bounciness));
        }

        item.Scale = Scale(item.Body.Feet);

        if (!WorldQuery.IsLost(_snapshot.Monitors, item.Body.Feet))
        {
            item.LostSeconds = 0;
            return false;
        }

        item.LostSeconds += dt;
        if (item.LostSeconds < _config.LostGraceSeconds || WorldQuery.Nearest(_snapshot.Monitors, item.Body.Feet) is not { } monitor)
        {
            return false;
        }

        // Respawn at the top of the nearest monitor, like a lost pet.
        var area = monitor.WorkArea;
        item.Body.Feet = new Vec2(area.Left + (area.Width / 2.0), area.Top + item.Size.Y);
        item.Body.Release(Vec2.Zero);
        item.LostSeconds = 0;
        return true;
    }

    /// <summary>The pet's physics with the furniture's size, its friction, and no climbing.</summary>
    private PetConfig PhysicsConfigFor(FurnitureSpec spec) => _config with
    {
        SizeDip = spec.HeightDip,
        ArtSizePx = spec.Smooth ? 0 : spec.HeightDip, // one art pixel per DIP: snaps to the same size as FurnitureItem.Size
        BodyHalfWidthFraction = spec.WidthDip / 2 / spec.HeightDip,
        GroundFrictionDip = _config.GroundFrictionDip * spec.Friction,
        EnableClimbing = false,
    };

    private double Scale(Vec2 at) =>
        (WorldQuery.MonitorAt(_snapshot.Monitors, at) ?? WorldQuery.Nearest(_snapshot.Monitors, at))?.Scale ?? 1;
}

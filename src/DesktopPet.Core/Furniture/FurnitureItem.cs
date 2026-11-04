using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;

namespace DesktopPet.Core.Furniture;

/// <summary>What to draw for one piece of furniture.</summary>
/// <param name="Feet">Bottom-centre, physical pixels.</param>
/// <param name="Size">Width and height, physical pixels.</param>
/// <param name="FrameIndex">Which of its art's frames.</param>
public readonly record struct FurnitureFrame(Vec2 Feet, Vec2 Size, int FrameIndex)
{
    public RectI Bounds => RectI.FromSize(
        (int)System.Math.Round(Feet.X - (Size.X / 2)), (int)System.Math.Round(Feet.Y - Size.Y),
        (int)System.Math.Round(Size.X), (int)System.Math.Round(Size.Y));
}

/// <summary>
/// One piece of furniture in the world: a physics body like the pet's (it stands on floors and window tops, rides
/// moving windows and falls when its window goes), plus the slots pets have reserved.
/// </summary>
public sealed class FurnitureItem
{
    private object?[] _slots;
    private double _animationTime;

    internal FurnitureItem(int id, FurnitureSpec spec)
    {
        Id = id;
        Spec = spec;
        _slots = new object?[spec.Slots];
    }

    /// <summary>Unique while the app runs; not saved.</summary>
    public int Id { get; }

    public FurnitureSpec Spec { get; private set; }

    public string Kind => Spec.Kind;

    /// <summary>DPI scale of the monitor it's on.</summary>
    public double Scale { get; internal set; } = 1;

    public Vec2 Feet => Body.Feet;

    public Vec2 Size => new(Spec.WidthDip * Scale, Spec.HeightDip * Scale);

    public double Left => Feet.X - (Size.X / 2);

    public double Right => Feet.X + (Size.X / 2);

    public BodyMode Mode => Body.Mode;

    public bool IsHeld => Body.Mode == BodyMode.Held;

    public bool IsGrounded => Body.Mode == BodyMode.Grounded;

    /// <summary>The surface it stands on, or null while airborne or held.</summary>
    public string? GroundKey => IsGrounded ? Body.GroundKey : null;

    /// <summary>Taken out of the world; pets heading for it give up.</summary>
    public bool IsRemoved { get; internal set; }

    /// <summary>Pets using or heading for it.</summary>
    public int Occupied => _slots.Count(s => s is not null);

    public FurnitureFrame Current { get; private set; }

    public FurnitureFrame Previous { get; private set; }

    internal PetBody Body { get; } = new();

    /// <summary>Physics tuning for its size (the pet's physics with its width and height).</summary>
    internal PetConfig? PhysicsConfig { get; set; }

    internal double LostSeconds { get; set; }

    /// <summary>The slot <paramref name="pet"/> holds, or -1.</summary>
    internal int SlotOf(object pet) => Array.IndexOf(_slots, pet);

    /// <summary>Reserves a free slot for <paramref name="pet"/> (or returns the one it holds); -1 if full.</summary>
    internal int Reserve(object pet)
    {
        var held = SlotOf(pet);
        if (held >= 0)
        {
            return held;
        }

        var free = Array.IndexOf(_slots, null);
        if (free >= 0)
        {
            _slots[free] = pet;
        }

        return free;
    }

    internal void Release(object pet)
    {
        Users.Remove(pet);
        var slot = SlotOf(pet);
        if (slot >= 0)
        {
            _slots[slot] = null;
        }
    }

    internal bool HasFreeSlot(object pet) => SlotOf(pet) >= 0 || Array.IndexOf(_slots, null) >= 0;

    /// <summary>Where slot <paramref name="slot"/>'s pet stands (feet x).</summary>
    internal double SlotX(int slot) => Feet.X + (Spec.SlotOffsetDip(slot) * Scale);

    /// <summary>Swaps in an edited definition of the same kind, keeping the reservations that still fit.</summary>
    internal void SetSpec(FurnitureSpec spec)
    {
        Spec = spec;
        PhysicsConfig = null;
        if (_slots.Length != spec.Slots)
        {
            var slots = new object?[spec.Slots];
            Array.Copy(_slots, slots, System.Math.Min(_slots.Length, slots.Length));
            _slots = slots;
        }
    }

    internal void Animate(double dt)
    {
        var playing = Spec.Animate switch
        {
            AnimateWhen.Moving => Body.Velocity.LengthSquared > 1,
            AnimateWhen.InUse => InUse,
            _ => true,
        };

        _animationTime = playing ? _animationTime + dt : 0;
    }

    /// <summary>Pets using it right now (not just heading for it).</summary>
    internal HashSet<object> Users { get; } = [];

    /// <summary>Someone is using it (for <see cref="AnimateWhen.InUse"/>).</summary>
    public bool InUse => Users.Count > 0;

    internal void UpdateFrame(bool snap)
    {
        Previous = Current;
        var frame = (int)(_animationTime * Spec.Fps) % System.Math.Max(1, Spec.Frames);
        Current = new FurnitureFrame(Body.Feet, Size, frame);
        if (snap)
        {
            Previous = Current;
        }
    }
}

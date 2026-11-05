using Deskling.Core.Abstractions;
using Deskling.Core.Animation;
using Deskling.Core.Furniture;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;
using Deskling.Core.World;

namespace Deskling.Core.Behavior;

/// <summary>Shared mutable state that behavior states read and act on. One per pet.</summary>
internal sealed class PetContext
{
    public PetContext(PetConfig config, IRandom random, FurnitureWorld furniture, object owner)
    {
        Config = config;
        Random = random;
        PushingEnabled = config.EnablePushing;
        Furniture = furniture;
        Owner = owner;
    }

    /// <summary>The furniture, shared with the other pets.</summary>
    public FurnitureWorld Furniture { get; }

    /// <summary>Who holds furniture slots for this pet (its simulation).</summary>
    public object Owner { get; }

    /// <summary>Simulation time, seconds (for retry timers).</summary>
    public double Time { get; set; }

    /// <summary>The trip to furniture under way (heading there or using it), if any.</summary>
    public Errand? Errand { get; set; }

    /// <summary>The use in progress while the pet is using furniture (stats gain from it).</summary>
    public FurnitureUse? ActiveUse { get; set; }

    /// <summary>The pen the pet is shut in, if the user dropped it into one.</summary>
    public FurnitureItem? Pen { get; set; }

    /// <summary>The pet was let go by the user and hasn't landed yet (landing in a pen shuts it in).</summary>
    public bool UserDropped { get; set; }

    /// <summary>The courtship under way (walking up to its partner, then gazing at it), if any.</summary>
    public Courtship? Courtship { get; set; }

    /// <summary>
    /// The pet swallowed a breeding pill and is going round the other pets (M15): where it's heading, set by
    /// <see cref="Breeding.BreedingDirector"/>. Kept through climbs, falls and being picked up, until the round ends.
    /// </summary>
    public Visit? Visit { get; set; }

    /// <summary>The pet is fixed (its traits): it never takes a breeding pill.</summary>
    public bool Fixed { get; set; }

    /// <summary>Whether it may take a breeding pill now: not fixed, breeding on, not already on a round.</summary>
    public bool MayTakePill => !Fixed && Config.Breeding.Enabled && Visit is null;

    /// <summary>Time (<see cref="Time"/>) before which it won't join another pet's game (<see cref="GroupPlay"/>).</summary>
    public double NoJoinUntil { get; set; }

    /// <summary>Furniture that couldn't be reached, by id, with the time it may be tried again.</summary>
    public Dictionary<int, double> Unreachable { get; } = [];

    /// <summary>Gives up the current trip to furniture, freeing its slot.</summary>
    public void CancelErrand()
    {
        if (Errand is { } errand)
        {
            errand.Item.Release(Owner);
            Errand = null;
        }

        ActiveUse = null;
    }

    /// <summary>
    /// What to do next when a behavior ends: carry on to the furniture it's heading for or the pet it's visiting, or
    /// pick by weight (which may start a new trip to furniture).
    /// </summary>
    public PetStateId PickNext() => OnTrip ? TripState : BehaviorPicker.Pick(this);

    /// <summary>On its way somewhere: to furniture, or round the other pets.</summary>
    public bool OnTrip => Errand is not null || Visit is not null;

    /// <summary>The state that carries the trip on (after a climb or a fall).</summary>
    public PetStateId TripState => Errand is not null ? PetStateId.GoToFurniture : PetStateId.Visit;

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

    public double SizePx => Config.SizePxAt(Scale);
    public double HalfWidthPx => SizePx * Config.BodyHalfWidthFraction;

    /// <summary>+1 facing right, -1 facing left.</summary>
    public int Direction => FacingLeft ? -1 : 1;
}

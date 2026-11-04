using System.Diagnostics;
using System.Windows.Threading;
using DesktopPet.App.Furniture;
using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Breeding;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Persistence;

namespace DesktopPet.App.Breeding;

/// <summary>
/// Runs breeding (M13) about ten times a second: ticks Core's <see cref="BreedingDirector"/> with every pet, hands each
/// mating to the app to lay an egg, and hatches eggs whose time has come. Glue only; the rules live in Core.
/// </summary>
internal sealed class BreedingHost : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    private readonly Func<IReadOnlyList<PetHost>> _pets;
    private readonly FurnitureHost _furniture;
    private readonly Action<Mating> _laid;
    private readonly Action<FurnitureItem> _hatch;
    private readonly Action<Saying> _said;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _sinceTick = Stopwatch.StartNew();
    private readonly SystemClock _clock = SystemClock.Instance;

    /// <param name="pets">The pets right now.</param>
    /// <param name="furniture">Where the eggs are.</param>
    /// <param name="laid">Two pets mated: lay the egg.</param>
    /// <param name="hatch">An egg is due: hatch it (the app removes it).</param>
    /// <param name="said">A pet said something on a breeding pill round: show it.</param>
    public BreedingHost(
        Func<IReadOnlyList<PetHost>> pets, FurnitureHost furniture, Action<Mating> laid, Action<FurnitureItem> hatch, Action<Saying> said)
    {
        _said = said;
        _pets = pets;
        _furniture = furniture;
        _laid = laid;
        _hatch = hatch;
        Director = new BreedingDirector(new SeededRandom(Environment.TickCount), _clock);
        _timer = new DispatcherTimer(Interval, DispatcherPriority.Background, (_, _) => Tick(), Dispatcher.CurrentDispatcher);
    }

    public BreedingDirector Director { get; }

    public BreedingRules Rules { get; set; } = new();

    /// <summary>The pets plus eggs that fit (the cap counts eggs).</summary>
    public bool RoomForEgg => _pets().Count + _furniture.EggCount < PetRoster.MaxPets;

    public void Start()
    {
        Tick(); // eggs that hatched while the app was closed come out right away
        _timer.Start();
    }

    /// <summary>A pet is being removed: any courtship it's in is off.</summary>
    public void Forget(int petId) => Director.Forget(petId, [.. _pets().Select(p => p.BreedingInfo)]);

    public void Dispose() => _timer.Stop();

    private void Tick()
    {
        var dt = System.Math.Min(_sinceTick.Elapsed.TotalSeconds, 1);
        _sinceTick.Restart();
        var pets = _pets();
        Director.Tick(dt, [.. pets.Select(p => p.BreedingInfo)], Rules, RoomForEgg);
        foreach (var saying in Director.TakeSayings())
        {
            _said(saying);
        }

        foreach (var mating in Director.TakeMatings())
        {
            _laid(mating);
        }

        var now = _clock.UtcNow;
        foreach (var egg in _furniture.World.EggItems.Where(e => e.Egg!.HatchAtUtc <= now && !e.IsHeld).ToList())
        {
            _hatch(egg);
        }
    }
}

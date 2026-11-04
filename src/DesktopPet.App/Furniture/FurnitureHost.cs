using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using DesktopPet.App.UI;
using DesktopPet.Core.Breeding;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App.Furniture;

/// <summary>
/// Owns the furniture: the shared <see cref="FurnitureWorld"/>, its loop, one window per piece and room.json.
/// Glue only; the rules live in Core.
/// </summary>
internal sealed class FurnitureHost : IDisposable
{
    /// <summary>More than this many pieces would mostly be clutter (and windows).</summary>
    public const int MaxItems = 24;

    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromMinutes(1);

    private readonly DesktopSource _desktop;
    private readonly RoomStore _store;
    private readonly GameLoop _loop;
    private readonly Dictionary<int, PetWindow> _windows = [];
    private readonly Stopwatch _sinceSave = Stopwatch.StartNew();
    private readonly Action<FurnitureItem> _menuRequested;
    private readonly Action _windowsAdded;
    private Dictionary<string, BitmapSource[]> _art = new(StringComparer.OrdinalIgnoreCase);
    private DesktopSnapshot _snapshot;
    private int _savedVersion;
    private bool _closed;
    private bool _rendering;

    /// <param name="config">Shared tuning (physics, drag threshold).</param>
    /// <param name="desktop">The desktop, shared with the pets.</param>
    /// <param name="store">room.json.</param>
    /// <param name="menuRequested">A piece was right-clicked.</param>
    /// <param name="windowsAdded">New furniture windows appeared (so the pets can be put back in front of them).</param>
    public FurnitureHost(PetConfig config, DesktopSource desktop, RoomStore store, Action<FurnitureItem> menuRequested, Action windowsAdded)
    {
        _desktop = desktop;
        _store = store;
        _menuRequested = menuRequested;
        _windowsAdded = windowsAdded;
        _snapshot = desktop.GetSnapshot();
        World = new FurnitureWorld(config);
        _loop = new GameLoop(new FixedStepper(config.StepSeconds, config.MaxStepsPerFrame), BeginFrame, Step, Render);
    }

    public FurnitureWorld World { get; }

    /// <summary>Pieces of furniture (eggs aren't furniture here: they count toward the pet cap instead).</summary>
    public int Count => World.Items.Count(i => i.Egg is null);

    /// <summary>Eggs waiting to hatch.</summary>
    public int EggCount => World.EggItems.Count();

    /// <summary>Lays an egg at <paramref name="feet"/> (physical pixels).</summary>
    public void AddEgg(Vec2 feet, EggInfo egg)
    {
        World.AddEgg(feet, egg);
        Render(alpha: 1);
        Save();
    }

    /// <summary>The kinds that can be placed, with their art. Furniture of kinds that are gone is removed.</summary>
    public void SetSpecs(IReadOnlyList<FurnitureSpec> specs, Dictionary<string, BitmapSource[]> art)
    {
        _art = art;
        World.SetSpecs(specs);
        Render(alpha: 1);
    }

    /// <summary>Puts back the furniture from room.json, then starts stepping.</summary>
    public void Start()
    {
        World.Load(_store.Load());
        World.Tick(0, _snapshot);
        _savedVersion = World.Version;
        _loop.Start();
        Render(alpha: 1);
    }

    public void Reconfigure(PetConfig config) => World.Reconfigure(config);

    /// <summary>Drops a new piece in at <paramref name="feet"/> (physical pixels).</summary>
    public void Add(string kind, Vec2 feet)
    {
        if (Count < MaxItems && World.Add(kind, feet) is not null)
        {
            Render(alpha: 1);
            Save();
        }
    }

    public void Remove(FurnitureItem item)
    {
        World.Remove(item);
        Render(alpha: 1);
        Save();
    }

    /// <summary>Removes all the furniture (not the eggs).</summary>
    public void RemoveAll()
    {
        foreach (var item in World.Items.Where(i => i.Egg is null).ToList())
        {
            World.Remove(item);
        }

        Render(alpha: 1);
        Save();
    }

    /// <summary>Writes room.json. Failures are swallowed: the furniture stays where it is.</summary>
    public void Save()
    {
        if (_closed)
        {
            return;
        }

        try
        {
            _store.Save(World.CreateSave());
            _savedVersion = World.Version;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"DesktopPet: saving room.json failed: {e.Message}");
        }

        _sinceSave.Restart();
    }

    /// <summary>What the debug overlay shows for each piece.</summary>
    public IReadOnlyList<DebugPet> DebugInfo =>
        [.. World.Items.Select(i => new DebugPet(
            i.Spec.Name, i.Current.Bounds, i.Egg is not null ? "egg" : i.Spec.Slots > 0 ? $"{i.Occupied}/{i.Spec.Slots}" : "pen", i.Mode, i.GroundKey))];

    /// <summary>Re-asserts that the furniture is on top of ordinary windows (pets go above it afterwards).</summary>
    public void BringToFront()
    {
        foreach (var window in _windows.Values)
        {
            window.BringToFront();
        }
    }

    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        Save();
        _closed = true;
        _loop.Dispose();
        foreach (var window in _windows.Values)
        {
            window.Close();
        }

        _windows.Clear();
    }

    private static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private void BeginFrame()
    {
        _snapshot = _desktop.GetSnapshot();
        if (World.IsDragging)
        {
            World.PointerMove(CursorSource.GetPosition(), Now());
        }
    }

    private void Step(double dt) => World.Tick(dt, _snapshot);

    private void Render(double alpha)
    {
        // Creating a window can run a frame of our own loop before it returns: a nested Render would make a second
        // window for the same piece, which then stays behind on screen, never moved or closed.
        if (_closed || _rendering)
        {
            return;
        }

        _rendering = true;
        try
        {
            RenderItems(alpha);
        }
        finally
        {
            _rendering = false;
        }
    }

    private void RenderItems(double alpha)
    {
        var added = false;
        foreach (var item in World.Items)
        {
            if (!_windows.TryGetValue(item.Id, out var window))
            {
                window = CreateWindow(item);
                _windows[item.Id] = window;
                added = true;
            }

            var frames = _art.TryGetValue(item.Kind, out var f) ? f : null;
            if (frames is not { Length: > 0 })
            {
                continue;
            }

            var current = item.Current;
            var feet = item.IsHeld ? current.Feet : Vec2.Lerp(item.Previous.Feet, current.Feet, alpha);
            var bitmap = frames[System.Math.Clamp(current.FrameIndex, 0, frames.Length - 1)];
            window.Present(new FurnitureFrame(feet, current.Size, current.FrameIndex).Bounds, bitmap, facingLeft: false, item.Spec.Smooth);
            if (!window.IsVisible)
            {
                window.Show();
            }
        }

        foreach (var id in _windows.Keys.Where(id => World.Find(id) is null).ToList())
        {
            _windows[id].Close();
            _windows.Remove(id);
        }

        if (added)
        {
            _windowsAdded();
        }

        // Save once something new has settled (not mid-drag or mid-fall), and every minute for things that ride windows.
        var settled = !World.IsDragging && World.Items.All(i => i.IsGrounded);
        if ((World.Version != _savedVersion && settled) || _sinceSave.Elapsed >= AutosaveInterval)
        {
            Save();
        }
    }

    private PetWindow CreateWindow(FurnitureItem item)
    {
        var window = new PetWindow { KeepOnTop = false, Title = item.Spec.Name };
        window.CreateHandle();
        window.PointerPressed += (_, _) => World.PointerDown(item, CursorSource.GetPosition(), Now());
        window.PointerReleased += (_, _) => World.PointerUp(CursorSource.GetPosition(), Now());
        window.PointerCanceled += (_, _) => World.PointerCancel();
        window.MenuRequested += (_, _) => _menuRequested(item);
        return window;
    }
}

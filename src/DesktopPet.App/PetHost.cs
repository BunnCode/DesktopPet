using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using DesktopPet.App.SpriteSheet;
using DesktopPet.App.UI;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Chat;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Traits;
using DesktopPet.Core.World;
using DesktopPet.Platform.Windows;
using Microsoft.Win32;

namespace DesktopPet.App;

/// <summary>
/// Owns one pet: simulation, sprites, window, loop, saving. Glue only; behavior lives in Core.
/// Several can run side by side; they share one <see cref="DesktopSource"/>.
/// </summary>
internal sealed class PetHost : IPetControls, IDisposable
{
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromMinutes(1);

    private readonly DesktopSource _desktop;
    private readonly ISaveStore _store;
    private readonly PetSimulation _simulation;
    private readonly PetWindow _window;
    private readonly GameLoop _loop;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Stopwatch _sinceSave = Stopwatch.StartNew();
    private SpriteAtlas _atlas;
    private DesktopSnapshot _snapshot;
    private bool _pointerActive;
    private bool _closed;

    /// <param name="id">Pet number (1, 2, …), its folder name; also picks its jacket colour.</param>
    /// <param name="config">Shared tuning.</param>
    /// <param name="traits">This pet's name and multipliers, applied on top of <paramref name="config"/>.</param>
    /// <param name="personality">This pet's personality text.</param>
    /// <param name="store">Where the pet is loaded from and saved to.</param>
    /// <param name="atlas">Sprites to draw with.</param>
    /// <param name="desktop">The desktop, shared by all pets.</param>
    /// <param name="furniture">The furniture, shared by all pets.</param>
    public PetHost(
        int id, PetConfig config, PetTraits traits, string personality, ISaveStore store, SpriteAtlas atlas, DesktopSource desktop,
        FurnitureWorld furniture)
    {
        Id = id;
        Personality = personality;
        _store = store;
        _desktop = desktop;
        _snapshot = _desktop.GetSnapshot();
        _simulation = new PetSimulation(config, _snapshot, save: _store.Load(), traits: traits, furniture: furniture);
        _atlas = atlas;

        _window = new PetWindow();
        _window.CreateHandle();
        _window.PointerPressed += OnPointerPressed;
        _window.PointerReleased += OnPointerReleased;
        _window.PointerCanceled += OnPointerCanceled;
        _window.MenuRequested += (_, _) => MenuRequested?.Invoke(this, EventArgs.Empty);
        Render(alpha: 1); // position and first frame before the window is visible

        _loop = new GameLoop(
            new FixedStepper(config.StepSeconds, config.MaxStepsPerFrame),
            beginFrame: BeginFrame,
            step: Step,
            render: Render);

        // Locked screen = user away: stats keep decaying but can't fall to critical.
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>The pet was right-clicked.</summary>
    public event EventHandler? MenuRequested;

    public int Id { get; }

    /// <summary>Built-in art colour scheme (jacket colour) for this pet.</summary>
    public int Variant => (Id - 1) % DefaultArt.VariantCount;

    public string Name => _simulation.Traits.Name;

    public PetTraits Traits => _simulation.Traits;

    /// <summary>The personality text from the pet's personality.txt.</summary>
    public string Personality { get; set; }

    public PetStats Stats => _simulation.Stats;

    /// <summary>Where the pet's feet are (physical pixels).</summary>
    public Vec2 Feet => _simulation.Current.FeetPos;

    /// <summary>The pet's sprite size (physical pixels).</summary>
    public double SizePx => _simulation.Current.Size.Y;

    public bool IsAsleep => _simulation.State == PetStateId.Sleep;

    /// <summary>Whether the pet may push windows around (moves the user's real windows).</summary>
    public bool PushingEnabled
    {
        get => _simulation.PushingEnabled;
        set => _simulation.PushingEnabled = value;
    }

    public StatRules Rules => _simulation.Config.Stats;

    /// <summary>The settings this pet runs on (shared ones with its traits applied).</summary>
    public PetConfig Config => _simulation.Config;

    /// <summary>The screen is locked: the user isn't there.</summary>
    public bool UserAway => _simulation.UserAway;

    /// <summary>The last click on the pet since the previous call (not drags), for its spoken reaction.</summary>
    public PetClick? TakeClick() => _simulation.TakeClick();

    /// <summary>The pet as chat sees it: who it is, how it feels, where it is, whether it can talk.</summary>
    public ChatPet ChatInfo
    {
        get
        {
            var current = _simulation.Current;
            var asleep = _simulation.State == PetStateId.Sleep || current.AnimationId == Animations.Sleep.Id;
            var window = _simulation.Mode == BodyMode.Grounded ? PlatformBuilder.WindowIdFromKey(_simulation.GroundKey) : null;
            var scale = current.Size.Y / System.Math.Max(1, _simulation.Config.SizeDip);
            var readable = _simulation.ReadableWindow(_simulation.Config.Chat.ReadReachDip);
            return new ChatPet(Id, Name, Personality, Stats, current.FeetPos, scale, !asleep, window, Traits.Chat, readable);
        }
    }

    /// <summary>What this pet sees and is doing, for the debug overlay.</summary>
    public (IReadOnlyList<Core.World.Platform> Platforms, IReadOnlyList<Wall> Walls, DesktopSnapshot Snapshot, DebugPet Pet) DebugInfo
    {
        get
        {
            var current = _simulation.Current;
            var pet = new DebugPet(Name, current.Bounds, current.State.ToString(), _simulation.Mode, _simulation.GroundKey);
            return (_simulation.Platforms, _simulation.Walls, _snapshot, pet);
        }
    }

    /// <summary>Applies edited settings live (the fixed step rate only changes on restart).</summary>
    public void Reconfigure(PetConfig config) => _simulation.Reconfigure(config);

    /// <summary>How many of its animations use custom art.</summary>
    public int CustomAnimations => _atlas.CustomCount;

    /// <summary>Applies edited traits live.</summary>
    public void SetTraits(PetTraits traits) => _simulation.SetTraits(traits);

    /// <summary>Switches to reloaded sprites.</summary>
    public void SetAtlas(SpriteAtlas atlas)
    {
        _atlas = atlas;
        Render(alpha: 1);
    }

    /// <summary>Drops the pet in from a point (a newly added pet arriving from the top of the screen).</summary>
    public void DropFrom(Vec2 feet) => _simulation.DropFrom(feet);

    public void BringToFront() => _window.BringToFront();

    public void Start()
    {
        _window.Show();
        _loop.Start();
    }

    public void Apply(PetAction action)
    {
        _simulation.Apply(action);
        Save();
    }

    /// <summary>Writes the save file. Failures (disk full, permissions) are swallowed: the pet keeps running.</summary>
    public void Save()
    {
        if (_closed)
        {
            return;
        }

        try
        {
            _store.Save(_simulation.CreateSave());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"DesktopPet: save failed: {e.Message}");
        }

        _sinceSave.Restart();
    }

    /// <summary>Stops the pet and closes its window, saving first unless it's being removed.</summary>
    public void Close(bool save)
    {
        if (_closed)
        {
            return;
        }

        if (save)
        {
            Save();
        }

        _closed = true;
        _simulation.LeaveFurniture();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _loop.Dispose();
        _window.Close();
    }

    public void Dispose() => Close(save: true);

    /// <summary>Seconds on a monotonic clock, for pointer timestamps.</summary>
    private static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    /// <summary>Physical-pixel cursor position (WPF mouse coordinates are DIPs relative to a moving window).</summary>
    private static Vec2 Cursor() => CursorSource.GetPosition();

    /// <summary>Raised on a SystemEvents thread; hop to the UI thread that owns the simulation.</summary>
    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        bool? away = e.Reason switch
        {
            SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect => true,
            SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect => false,
            _ => null,
        };

        if (away is { } value)
        {
            _dispatcher.BeginInvoke(() => _simulation.UserAway = value);
        }
    }

    private void BeginFrame()
    {
        _snapshot = _desktop.GetSnapshot();

        // During a press, sample the cursor every frame: detects the drag threshold and gives the
        // throw estimator evenly spaced samples, including "held still" ones.
        if (_pointerActive)
        {
            _simulation.PointerMove(Cursor(), Now());
        }
    }

    private void OnPointerPressed(object? sender, EventArgs e)
    {
        _pointerActive = true;
        _simulation.PointerDown(Cursor(), Now());
    }

    private void OnPointerReleased(object? sender, EventArgs e)
    {
        _pointerActive = false;
        _simulation.PointerUp(Cursor(), Now());
    }

    private void OnPointerCanceled(object? sender, EventArgs e)
    {
        _pointerActive = false;
        _simulation.PointerCancel();
    }

    private void Render(double alpha)
    {
        var current = _simulation.Current;

        // A held pet must stick to the cursor: draw the latest position, no interpolation lag.
        var feet = _simulation.IsHeld
            ? current.FeetPos
            : Vec2.Lerp(_simulation.Previous.FeetPos, current.FeetPos, alpha);

        var frame = _atlas.Get(current.AnimationId, current.FrameIndex);
        _window.Present(
            PetFrame.SpriteRect(feet + new Vec2(0, current.SinkY), current.Size),
            frame.Bitmap,
            current.FacingLeft != frame.FacesLeft, // mirror when the art faces the other way
            frame.Smooth);

        if (_sinceSave.Elapsed >= AutosaveInterval)
        {
            Save();
        }
    }

    /// <summary>One simulation step, then carry out any window moves it asked for (pushing).</summary>
    private void Step(double dt)
    {
        _simulation.Tick(dt, _snapshot);
        foreach (var command in _simulation.TakeCommands())
        {
            WindowManipulator.MoveBy(command.WindowId, command.Dx, command.Dy);
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using DesktopPet.App.SpriteSheet;
using DesktopPet.App.UI;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Behavior;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.World;
using DesktopPet.Platform.Windows;
using Microsoft.Win32;

namespace DesktopPet.App;

/// <summary>Owns one pet: simulation, sprites, window, loop, saving. Glue only; behavior lives in Core.</summary>
internal sealed class PetHost : IDisposable
{
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromMinutes(1);

    private readonly DesktopSource _desktop = new();
    private readonly PetConfig _config;
    private readonly ISaveStore _store;
    private readonly PetSimulation _simulation;
    private readonly SpriteAtlas _atlas;
    private readonly PetWindow _window;
    private readonly GameLoop _loop;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Stopwatch _sinceSave = Stopwatch.StartNew();
    private DesktopSnapshot _snapshot;
    private bool _pointerActive;
    private DebugOverlay? _overlay;
    private int _frameCount;

    /// <param name="config">Tuning.</param>
    /// <param name="store">Where the pet is loaded from and saved to.</param>
    public PetHost(PetConfig config, ISaveStore store)
    {
        _config = config;
        _store = store;
        _snapshot = _desktop.GetSnapshot();
        _simulation = new PetSimulation(config, _snapshot, save: _store.Load());
        _atlas = SpriteAtlas.CreatePlaceholder(Animations.All);

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
            step: dt => _simulation.Tick(dt, _snapshot),
            render: Render);

        // Locked screen = user away: stats keep decaying but can't fall to critical.
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>The pet was right-clicked.</summary>
    public event EventHandler? MenuRequested;

    public PetStats Stats => _simulation.Stats;

    public bool IsAsleep => _simulation.State == PetStateId.Sleep;

    public StatRules Rules => _config.Stats;

    /// <summary>Shows/hides the click-through overlay that draws platforms and pet state.</summary>
    public bool DebugOverlayEnabled
    {
        get => _overlay is not null;
        set
        {
            if (value == DebugOverlayEnabled)
            {
                return;
            }

            if (value)
            {
                _overlay = new DebugOverlay();
                UpdateOverlay();
                _window.BringToFront(); // keep the pet above the overlay
            }
            else
            {
                _overlay!.Dispose();
                _overlay = null;
            }
        }
    }

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

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _loop.Dispose();
        Save();
        _overlay?.Dispose();
        _window.Close();
    }

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

        _window.Present(
            PetFrame.SpriteRect(feet, current.Size),
            _atlas.Get(current.AnimationId, current.FrameIndex),
            current.FacingLeft);

        // The overlay re-renders full-screen layered windows in software; ~15 Hz is plenty for debugging.
        if (_overlay is not null && ++_frameCount % 4 == 0)
        {
            UpdateOverlay();
        }

        if (_sinceSave.Elapsed >= AutosaveInterval)
        {
            Save();
        }
    }

    private void UpdateOverlay()
    {
        var current = _simulation.Current;
        _overlay?.Update(
            _snapshot.Monitors,
            new DebugView(
                _simulation.Platforms, current.Bounds, current.State.ToString(), _simulation.Mode, _simulation.GroundKey,
                _snapshot.Windows.Count));
    }
}

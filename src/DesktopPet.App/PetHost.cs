using System.Diagnostics;
using DesktopPet.App.SpriteSheet;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App;

/// <summary>Owns one pet: simulation, sprites, window and loop. Glue only; behavior lives in Core.</summary>
internal sealed class PetHost : IDisposable
{
    private readonly DesktopSource _desktop = new();
    private readonly PetSimulation _simulation;
    private readonly SpriteAtlas _atlas;
    private readonly PetWindow _window;
    private readonly GameLoop _loop;
    private DesktopSnapshot _snapshot;
    private bool _pointerActive;

    public PetHost()
    {
        var config = PetConfig.Default;
        _snapshot = _desktop.GetSnapshot();
        _simulation = new PetSimulation(config, _snapshot);
        _atlas = SpriteAtlas.CreatePlaceholder(Animations.All);

        _window = new PetWindow();
        _window.CreateHandle();
        _window.PointerPressed += OnPointerPressed;
        _window.PointerReleased += OnPointerReleased;
        _window.PointerCanceled += OnPointerCanceled;
        Render(alpha: 1); // position and first frame before the window is visible

        _loop = new GameLoop(
            new FixedStepper(config.StepSeconds, config.MaxStepsPerFrame),
            beginFrame: BeginFrame,
            step: dt => _simulation.Tick(dt, _snapshot),
            render: Render);
    }

    public void Start()
    {
        _window.Show();
        _loop.Start();
    }

    public void Dispose()
    {
        _loop.Dispose();
        _window.Close();
    }

    /// <summary>Seconds on a monotonic clock, for pointer timestamps.</summary>
    private static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    /// <summary>Physical-pixel cursor position (WPF mouse coordinates are DIPs relative to a moving window).</summary>
    private static Vec2 Cursor() => CursorSource.GetPosition();

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
    }
}

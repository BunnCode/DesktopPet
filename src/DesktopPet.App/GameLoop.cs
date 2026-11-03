using System.Windows.Media;
using DesktopPet.Core.Simulation;

namespace DesktopPet.App;

/// <summary>
/// Drives the simulation from WPF's per-frame <see cref="CompositionTarget.Rendering"/> (vsync-paced),
/// running fixed steps via <see cref="FixedStepper"/> and then rendering with the leftover alpha.
/// </summary>
internal sealed class GameLoop : IDisposable
{
    private readonly FixedStepper _stepper;
    private readonly Action _beginFrame;
    private readonly Action<double> _step;
    private readonly Action<double> _render;
    private TimeSpan? _lastRenderingTime;
    private bool _running;

    /// <param name="stepper">Fixed-step accumulator.</param>
    /// <param name="beginFrame">Once per frame before stepping (e.g. sample the desktop).</param>
    /// <param name="step">One fixed simulation step of the given seconds.</param>
    /// <param name="render">Draw with the interpolation alpha in [0, 1).</param>
    public GameLoop(FixedStepper stepper, Action beginFrame, Action<double> step, Action<double> render)
    {
        _stepper = stepper;
        _beginFrame = beginFrame;
        _step = step;
        _render = render;
    }

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _lastRenderingTime = null;
        _stepper.Reset();
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    public void Dispose() => Stop();

    private void OnRendering(object? sender, EventArgs e)
    {
        // Rendering can fire more than once per frame; RenderingTime only changes once per frame.
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (_lastRenderingTime is { } last)
        {
            if (now == last)
            {
                return;
            }

            var steps = _stepper.Advance((now - last).TotalSeconds);
            if (steps > 0)
            {
                _beginFrame();
            }

            for (var i = 0; i < steps; i++)
            {
                _step(_stepper.StepSeconds);
            }
        }

        _lastRenderingTime = now;
        _render(_stepper.Alpha);
    }
}

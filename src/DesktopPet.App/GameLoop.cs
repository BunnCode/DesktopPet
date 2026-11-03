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
    private readonly Action<double> _step;
    private readonly Action<double> _render;
    private TimeSpan? _lastRenderingTime;
    private bool _running;

    public GameLoop(FixedStepper stepper, Action<double> step, Action<double> render)
    {
        _stepper = stepper;
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
            for (var i = 0; i < steps; i++)
            {
                _step(_stepper.StepSeconds);
            }
        }

        _lastRenderingTime = now;
        _render(_stepper.Alpha);
    }
}

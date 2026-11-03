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

    public PetHost()
    {
        var config = PetConfig.Default;
        _snapshot = _desktop.GetSnapshot();
        _simulation = new PetSimulation(config, _snapshot);
        _atlas = SpriteAtlas.CreatePlaceholder(Animations.All);

        _window = new PetWindow();
        _window.CreateHandle();
        Render(alpha: 1); // position and first frame before the window is visible

        _loop = new GameLoop(
            new FixedStepper(config.StepSeconds, config.MaxStepsPerFrame),
            beginFrame: () => _snapshot = _desktop.GetSnapshot(),
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

    private void Render(double alpha)
    {
        var current = _simulation.Current;
        var feet = Vec2.Lerp(_simulation.Previous.FeetPos, current.FeetPos, alpha);
        _window.Present(
            PetFrame.SpriteRect(feet, current.Size),
            _atlas.Get(current.AnimationId, current.FrameIndex),
            current.FacingLeft);
    }
}

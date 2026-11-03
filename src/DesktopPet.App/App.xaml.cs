using System.Diagnostics.CodeAnalysis;
using System.Windows;
using DesktopPet.App.SpriteSheet;
using DesktopPet.App.UI;

namespace DesktopPet.App;

/// <summary>Composition root. No main window: the pet window plus a tray icon; exit via the tray.</summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF Application can't be IDisposable; fields are disposed in OnExit.")]
public partial class App : Application
{
    private Mutex? _singleInstance;
    private PetHost? _pet;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\DesktopPet.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        _pet = new PetHost();
        var pet = _pet;
        _tray = new TrayIcon(
            PlaceholderArt.TrayIconPixels(),
            PlaceholderArt.FrameSize,
            onExit: Shutdown,
            onDebugOverlay: enabled => pet.DebugOverlayEnabled = enabled);
        _pet.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pet?.Dispose();
        _tray?.Dispose();
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }
}

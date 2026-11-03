using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using DesktopPet.App.SpriteSheet;
using DesktopPet.App.UI;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;

namespace DesktopPet.App;

/// <summary>Composition root. No main window: the pet window plus a tray icon; exit via the menu.</summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF Application can't be IDisposable; fields are disposed in OnExit.")]
public partial class App : Application
{
    private Mutex? _singleInstance;
    private PetHost? _pet;
    private PetMenu? _menu;
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

        var savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "save.json");
        var pet = _pet = new PetHost(PetConfig.Default, new JsonFileSaveStore(savePath));
        _menu = new PetMenu(
            stats: () => pet.Stats,
            isAsleep: () => pet.IsAsleep,
            rules: pet.Rules,
            act: pet.Apply,
            onDebugOverlay: enabled => pet.DebugOverlayEnabled = enabled,
            pushingEnabled: pet.PushingEnabled,
            onPushing: enabled => pet.PushingEnabled = enabled,
            onExit: Shutdown);
        _tray = new TrayIcon(PlaceholderArt.TrayIconPixels(), PlaceholderArt.FrameSize, _menu.Strip);

        var menu = _menu;
        pet.MenuRequested += (_, _) => menu.ShowAt(System.Windows.Forms.Cursor.Position);
        pet.Start();
    }

    /// <summary>Windows is logging off or shutting down: save now, OnExit may not get the chance.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _pet?.Save();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pet?.Dispose(); // saves
        _tray?.Dispose();
        _menu?.Dispose();
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }
}

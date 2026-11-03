using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DesktopPet.App.Customization;
using DesktopPet.App.SpriteSheet;
using DesktopPet.App.UI;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Rendering;
using DesktopPet.Core.Simulation;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App;

/// <summary>
/// Composition root. No main window: one window per pet plus a tray icon; exit via the menu.
/// Pets come and go with their save files; settings and sprites come from the data folder and reload live.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF Application can't be IDisposable; fields are disposed in OnExit.")]
public partial class App : Application
{
    private static readonly TimeSpan OverlayInterval = TimeSpan.FromMilliseconds(66); // ~15 Hz is plenty for debugging

    private readonly DataFolder _data = DataFolder.Default;
    private readonly List<PetHost> _pets = [];
    private readonly Random _random = new();
    private Mutex? _singleInstance;
    private DesktopSource? _desktop;
    private PetConfig _config = PetConfig.Default;
    private bool _pushingEnabled;
    private PetHost? _lastClicked;
    private PetMenu? _menu;
    private TrayIcon? _tray;
    private CustomizationWatcher? _watcher;
    private DebugOverlay? _overlay;
    private DispatcherTimer? _overlayTimer;

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

        _data.EnsureExists();
        var settings = _data.LoadSettings();
        _config = settings.Config ?? PetConfig.Default;
        _pushingEnabled = _config.EnablePushing;
        _desktop = new DesktopSource();

        // The first pet arrives at the top centre as always; the others drop in at random spots instead of in a pile.
        SpriteSheetSpec? sprites = null;
        foreach (var id in _data.StartupPets())
        {
            sprites = StartPet(id, dropIn: _pets.Count > 0);
        }

        _menu = new PetMenu(
            defaultTarget: () => _lastClicked ?? _pets[0],
            canAdd: () => _pets.Count < PetRoster.MaxPets,
            canRemove: () => _pets.Count > 1,
            onAdd: AddPet,
            onRemove: pet => RemovePet((PetHost)pet),
            pushingEnabled: _pushingEnabled,
            onPushing: SetPushing,
            customize: (EditSettings, () => DataFolder.Open(_data.Root), ExportSpriteTemplate),
            onDebugOverlay: SetDebugOverlay,
            onExit: Shutdown);
        _tray = new TrayIcon(PlaceholderArt.TrayIconPixels(), PlaceholderArt.FrameSize, _menu.Strip);

        if (settings.Errors.Count > 0)
        {
            ReportProblems("Settings have problems, so the defaults are used", settings.Errors);
        }

        ReportSprites(sprites, reloaded: false);
        _watcher = new CustomizationWatcher(_data, OnSettingsChanged, OnSpritesChanged);
    }

    /// <summary>Windows is logging off or shutting down: save now, OnExit may not get the chance.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        foreach (var pet in _pets)
        {
            pet.Save();
        }

        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        SetDebugOverlay(false);
        foreach (var pet in _pets)
        {
            pet.Dispose(); // saves
        }

        _tray?.Dispose();
        _menu?.Dispose();
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }

    // ---- Pets ----

    /// <summary>Starts pet <paramref name="id"/> from its save (or fresh). Returns its sprite spec for reporting.</summary>
    private SpriteSheetSpec? StartPet(int id, bool dropIn)
    {
        var variant = (id - 1) % PlaceholderArt.VariantCount;
        var (atlas, spec) = SpriteAtlas.Load(_data.Sprites, Animations.All, variant);
        var pet = new PetHost(id, _config, new JsonFileSaveStore(_data.SavePath(id)), atlas, _desktop!)
        {
            PushingEnabled = _pushingEnabled,
        };
        pet.MenuRequested += (_, _) =>
        {
            _lastClicked = pet;
            _menu?.ShowFor(pet, System.Windows.Forms.Cursor.Position);
        };

        if (dropIn)
        {
            pet.DropFrom(RandomDropPoint());
        }

        pet.Start();
        pet.Save(); // a new pet exists from now on, even if the app is killed
        _pets.Add(pet);
        return spec;
    }

    private void AddPet()
    {
        if (_pets.Count >= PetRoster.MaxPets)
        {
            return;
        }

        // Also skip ids of save files that exist but weren't loaded (beyond the cap), so none gets overwritten.
        var id = PetRoster.NextId(_pets.Select(p => p.Id).Concat(_data.StartupPets()));
        StartPet(id, dropIn: true); // shown last, so it's above the debug overlay if that's on
    }

    /// <summary>Closes the pet and deletes its save, so it doesn't come back next time.</summary>
    private void RemovePet(PetHost pet)
    {
        if (_pets.Count <= 1 || !_pets.Remove(pet))
        {
            return;
        }

        pet.Close(save: false);
        if (_lastClicked == pet)
        {
            _lastClicked = null;
        }

        try
        {
            File.Delete(_data.SavePath(pet.Id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportProblems($"{pet.Name}'s save couldn't be deleted", [ex.Message]);
        }
    }

    /// <summary>Somewhere along the top of the primary monitor's work area, so new pets don't all land in one spot.</summary>
    private Vec2 RandomDropPoint()
    {
        var monitors = _desktop!.GetSnapshot().Monitors;
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? (monitors.Count > 0 ? monitors[0] : null);
        if (primary is null)
        {
            return new Vec2(100, 100);
        }

        var area = primary.WorkArea;
        var size = _config.SizeDip * primary.Scale;
#pragma warning disable CA5394 // not security-relevant
        var x = area.Left + size + (_random.NextDouble() * System.Math.Max(1, area.Width - (2 * size)));
#pragma warning restore CA5394
        return new Vec2(x, area.Top + size);
    }

    private void SetPushing(bool enabled)
    {
        _pushingEnabled = enabled;
        foreach (var pet in _pets)
        {
            pet.PushingEnabled = enabled;
        }
    }

    // ---- Debug overlay (one for all pets) ----

    private void SetDebugOverlay(bool enabled)
    {
        if (enabled == (_overlay is not null))
        {
            return;
        }

        if (enabled)
        {
            _overlay = new DebugOverlay();
            _overlayTimer = new DispatcherTimer(OverlayInterval, DispatcherPriority.Background, (_, _) => UpdateOverlay(), Dispatcher);
            UpdateOverlay();
            foreach (var pet in _pets)
            {
                pet.BringToFront(); // keep pets above the overlay
            }
        }
        else
        {
            _overlayTimer?.Stop();
            _overlayTimer = null;
            _overlay?.Dispose();
            _overlay = null;
        }
    }

    private void UpdateOverlay()
    {
        if (_overlay is null || _pets.Count == 0)
        {
            return;
        }

        var infos = _pets.Select(p => p.DebugInfo).ToList();
        var world = infos[0]; // every pet sees the same desktop
        _overlay.Update(
            world.Snapshot.Monitors,
            new DebugView(world.Platforms, world.Walls, [.. infos.Select(i => i.Pet)], world.Snapshot.Windows.Count));
    }

    // ---- Customization ----

    private void OnSettingsChanged()
    {
        var settings = _data.LoadSettings();
        if (settings.Config is { } config)
        {
            _config = config;
            foreach (var pet in _pets)
            {
                pet.Reconfigure(config);
            }

            _tray?.Notify("Settings applied", "Your changes to settings.json are live.");
        }
        else
        {
            ReportProblems("Settings not applied (the previous settings stay in force)", settings.Errors);
        }
    }

    private void OnSpritesChanged()
    {
        SpriteSheetSpec? spec = null;
        foreach (var pet in _pets)
        {
            var (atlas, s) = SpriteAtlas.Load(_data.Sprites, Animations.All, pet.Variant);
            pet.SetAtlas(atlas);
            spec = s;
        }

        ReportSprites(spec, reloaded: true);
    }

    private void EditSettings()
    {
        _data.LoadSettings(); // writes the defaults if the file doesn't exist yet
        DataFolder.Open(_data.Settings);
    }

    private void ExportSpriteTemplate()
    {
        SpriteTemplateExporter.Export(_data.SpriteTemplate, Animations.All);
        DataFolder.Open(_data.SpriteTemplate);
        _tray?.Notify("Sprite template exported", "Copy template.png and atlas.json into the sprites folder and paint over them.");
    }

    private void ReportSprites(SpriteSheetSpec? spec, bool reloaded)
    {
        if (spec is null)
        {
            if (reloaded)
            {
                _tray?.Notify("Sprites", "No atlas.json in the sprites folder: using the placeholder art.");
            }

            return;
        }

        if (spec.Problems.Count > 0)
        {
            ReportProblems("Some sprites couldn't be used (placeholders shown instead)", spec.Problems);
        }
        else if (reloaded)
        {
            var custom = spec.Animations.Count;
            _tray?.Notify("Sprites reloaded", $"{custom} animation{(custom == 1 ? "" : "s")} with your art, {spec.UsingPlaceholder.Count} with placeholders.");
        }
    }

    /// <summary>Shows the first problems in a notification and writes all of them to problems.txt.</summary>
    private void ReportProblems(string heading, IReadOnlyList<string> problems)
    {
        _data.WriteProblems(heading, problems);
        var shown = string.Join("\n", problems.Take(2));
        var more = problems.Count > 2 ? $"\n(+{problems.Count - 2} more in problems.txt)" : string.Empty;
        _tray?.Notify(heading, shown + more, warning: true);
    }
}

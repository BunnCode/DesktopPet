using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.App.Chat;
using DesktopPet.App.Customization;
using DesktopPet.App.Furniture;
using DesktopPet.App.SpriteSheet;
using DesktopPet.App.UI;
using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Traits;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App;

/// <summary>
/// Composition root. No main window: one window per pet plus a tray icon; exit via the menu.
/// Pets come and go with their folders (pets\n\). Settings, shared sprites, furniture and each pet's traits, personality
/// and own sprites come from the data folder and reload live. The furniture is shared by all pets.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF Application can't be IDisposable; fields are disposed in OnExit.")]
public partial class App : Application
{
    private static readonly TimeSpan OverlayInterval = TimeSpan.FromMilliseconds(66); // ~15 Hz is plenty for debugging

    private readonly DataFolder _data = DataFolder.Default;
    private readonly List<PetHost> _pets = [];
    private readonly Random _random = new();
    private readonly SeededRandom _traitRandom = new(Environment.TickCount);
    private Mutex? _singleInstance;
    private DesktopSource? _desktop;
    private PetConfig _config = PetConfig.Default;
    private bool _pushingEnabled;
    private PetHost? _lastClicked;
    private PetMenu? _menu;
    private TrayIcon? _tray;
    private CustomizationWatcher? _watcher;
    private DebugOverlay? _overlay;
    private FurnitureHost? _furniture;
    private ChatHost? _chat;
    private Dictionary<string, FurnitureSpec> _furnitureSpecs = new(StringComparer.OrdinalIgnoreCase);
    private System.Windows.Forms.ContextMenuStrip? _furnitureMenu;
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
        var problems = new List<string>(_data.MigrateLegacySaves());
        var settings = _data.LoadSettings();
        _config = settings.Config ?? PetConfig.Default;
        _pushingEnabled = _config.EnablePushing;
        _desktop = new DesktopSource();

        // Furniture first: the pets share its world, and its windows go behind theirs.
        problems.AddRange(_data.WriteDefaultFurniture());
        _furniture = new FurnitureHost(_config, _desktop, new RoomStore(_data.Room), ShowFurnitureMenu, BringPetsToFront);
        problems.AddRange(LoadFurniture());
        _furniture.Start();

        // Read every pet's traits first, so names rolled for pets without a traits.json avoid all the existing ones.
        var startup = _data.StartupPets().Select(id => (Id: id, Traits: _data.LoadTraits(id))).ToList();
        var names = new List<string>();
        foreach (var (id, loaded) in startup)
        {
            if (loaded?.Traits is { } traits)
            {
                names.Add(traits.Name);
            }
            else if (loaded is not null)
            {
                problems.AddRange(loaded.Errors.Select(error => $@"pets\{id}\traits.json: {error}"));
            }
        }

        // The first pet arrives at the top centre as always; the others drop in at random spots instead of in a pile.
        foreach (var (id, loaded) in startup)
        {
            problems.AddRange(StartPet(id, loaded, names, dropIn: _pets.Count > 0));
        }

        _menu = new PetMenu(
            defaultTarget: () => _lastClicked ?? _pets[0],
            canAdd: () => _pets.Count < PetRoster.MaxPets,
            canRemove: () => _pets.Count > 1,
            onAdd: AddPet,
            onRemove: pet => RemovePet((PetHost)pet),
            petFiles: (pet => EditTraits((PetHost)pet), pet => DataFolder.Open(_data.PetFolder(((PetHost)pet).Id))),
            furniture: new FurnitureActions(
                Kinds: () => [.. _furnitureSpecs.Values.Select(s => (s.Kind, s.Name))],
                CanAdd: () => _furniture!.Count < FurnitureHost.MaxItems,
                Count: () => _furniture!.Count,
                Add: (pet, kind) => AddFurniture((PetHost)pet, kind),
                RemoveAll: () => _furniture!.RemoveAll(),
                OpenFolder: () => DataFolder.Open(_data.Furniture)),
            pushingEnabled: _pushingEnabled,
            onPushing: SetPushing,
            chat: (_config.Chat.Enabled, enabled => _chat!.Enabled = enabled),
            notes: (_config.Chat.NotesEnabled, enabled => _chat!.NotesEnabled = enabled),
            reading: (_config.Chat.ReadingEnabled, enabled => _chat!.ReadingEnabled = enabled),
            customize: (EditSettings, () => DataFolder.Open(_data.Root), ExportSpriteTemplate),
            onDebugOverlay: SetDebugOverlay,
            onExit: Shutdown);
        _tray = new TrayIcon(DefaultArt.TrayIconPixels(), DefaultArt.FrameSize, _menu.Strip);

        // Chat last: its notifications need the tray, and the model loads in the background while the pets run.
        _chat = new ChatHost(() => _pets, _desktop, (title, text, warning) => _tray?.Notify(title, text, warning))
        {
            Rules = _config.Chat,
            Enabled = _config.Chat.Enabled,
            NotesEnabled = _config.Chat.NotesEnabled,
            ReadingEnabled = _config.Chat.ReadingEnabled,
        };
        var (modelPath, searched) = _data.FindModel(_config.Chat.ModelFile);
        _chat.Start(modelPath, searched);

        if (settings.Errors.Count > 0)
        {
            problems.InsertRange(0, settings.Errors.Select(error => $"settings.json (the defaults are used): {error}"));
        }

        if (problems.Count > 0)
        {
            ReportProblems("Some files have problems", [.. problems.Distinct()]);
        }

        _watcher = new CustomizationWatcher(_data, OnSettingsChanged, OnSpritesChanged, OnPetChanged, OnFurnitureChanged);
    }

    /// <summary>Windows is logging off or shutting down: save now, OnExit may not get the chance.</summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        foreach (var pet in _pets)
        {
            pet.Save();
        }

        _furniture?.Save();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        SetDebugOverlay(false);
        _chat?.Dispose();
        foreach (var pet in _pets)
        {
            pet.Dispose(); // saves
        }

        _furniture?.Dispose(); // saves room.json
        _furnitureMenu?.Dispose();
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

    /// <summary>
    /// Starts pet <paramref name="id"/> from its folder: its save (or fresh), its traits (rolled and written if it has
    /// no traits.json; average ones while that file is broken), its personality (written from the traits if missing)
    /// and its sprites. Returns the problems found, for reporting.
    /// </summary>
    /// <param name="id">The pet's id (its folder name).</param>
    /// <param name="loaded">Its traits.json as read, or null if it has none.</param>
    /// <param name="names">Names of all pets, including ones not started yet; a newly rolled name is added.</param>
    /// <param name="dropIn">Drop in at a random spot rather than at the top centre.</param>
    private List<string> StartPet(int id, TraitsParseResult? loaded, List<string> names, bool dropIn)
    {
        var problems = new List<string>();
        _data.EnsurePetFolder(id);
        var traits = loaded?.Traits;
        if (loaded is null)
        {
            var rolled = TraitRoller.Roll(_config, _traitRandom, names);
            names.Add(rolled.Name);
            TryWrite(() => _data.WriteTraits(id, rolled), problems);
            traits = rolled;
        }

        traits ??= PetTraits.Neutral($"Pet {id}"); // traits.json is broken (reported by the caller)
        var personality = _data.LoadPersonality(id);
        if (personality is null)
        {
            var described = Personality.Describe(traits);
            TryWrite(() => _data.WritePersonality(id, described), problems);
            personality = described;
        }

        var variant = (id - 1) % DefaultArt.VariantCount;
        var (atlas, layers) = SpriteAtlas.Load(_data.SpriteFolders(id), Animations.All, variant);
        problems.AddRange(SpriteProblems(layers, traits.Name));

        var pet = new PetHost(id, _config, traits, personality, new JsonFileSaveStore(_data.SavePath(id)), atlas, _desktop!, _furniture!.World)
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
        return problems;
    }

    private void AddPet()
    {
        if (_pets.Count >= PetRoster.MaxPets)
        {
            return;
        }

        // Also skip ids of pet folders that exist but weren't loaded (beyond the cap), so none gets overwritten.
        var id = PetRoster.NextId(_pets.Select(p => p.Id).Concat(_data.ExistingPets()));
        var names = _pets.Select(p => p.Name).ToList();
        var problems = StartPet(id, loaded: null, names, dropIn: true); // shown last, so it's above the debug overlay if that's on
        if (problems.Count > 0)
        {
            ReportProblems("The new pet's files have problems", problems);
        }
        else
        {
            var pet = _pets[^1];
            _tray?.Notify($"Meet {pet.Name}", pet.Personality);
        }
    }

    /// <summary>Closes the pet and moves its folder to the Recycle Bin, so it doesn't come back next time.</summary>
    private void RemovePet(PetHost pet)
    {
        if (_pets.Count <= 1 || !_pets.Remove(pet))
        {
            return;
        }

        _chat?.Forget(pet.Id);
        pet.Close(save: false);
        if (_lastClicked == pet)
        {
            _lastClicked = null;
        }

        try
        {
            _data.RecyclePetFolder(pet.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            ReportProblems($"{pet.Name}'s folder couldn't be removed", [ex.Message]);
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

    private void BringPetsToFront()
    {
        foreach (var pet in _pets)
        {
            pet.BringToFront();
        }

        _chat?.BringToFront();
    }

    // ---- Furniture (shared by all pets) ----

    /// <summary>
    /// Reads the furniture folder: every definition and its art. A broken definition keeps the last good one (or, for a
    /// default kind, the default), so a typo doesn't make that furniture vanish. Returns the problems found.
    /// </summary>
    private List<string> LoadFurniture()
    {
        var problems = new List<string>();
        var specs = new List<FurnitureSpec>();
        foreach (var (kind, result) in _data.LoadFurniture([.. Animations.All.Select(a => a.Id)]))
        {
            if (result.Spec is { } spec)
            {
                specs.Add(spec);
                continue;
            }

            problems.AddRange(result.Errors.Select(error => $@"furniture\{kind}.json: {error}"));
            var fallback = _furnitureSpecs.GetValueOrDefault(kind) ?? DefaultFurniture.All.FirstOrDefault(d => d.Kind == kind);
            if (fallback is not null)
            {
                specs.Add(fallback);
            }
        }

        var art = new Dictionary<string, BitmapSource[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            var (frames, problem) = FurnitureArt.Load(_data.Furniture, spec);
            art[spec.Kind] = frames;
            if (problem is not null)
            {
                problems.Add($@"furniture\{spec.Kind}: {problem}");
            }
        }

        _furnitureSpecs = specs.ToDictionary(s => s.Kind, StringComparer.OrdinalIgnoreCase);
        _furniture!.SetSpecs(specs, art);
        return problems;
    }

    private void OnFurnitureChanged()
    {
        var problems = LoadFurniture();
        if (problems.Count > 0)
        {
            ReportProblems("Some furniture couldn't be used as it is", problems);
        }
        else
        {
            _tray?.Notify("Furniture reloaded", $"{_furnitureSpecs.Count} kinds of furniture.");
        }
    }

    /// <summary>Drops a piece of furniture in just above the pet, so it lands beside it on whatever it's standing on.</summary>
    private void AddFurniture(PetHost pet, string kind)
    {
        var feet = pet.Feet;
        _furniture!.Add(kind, new Vec2(feet.X, feet.Y - pet.SizePx - 4));
    }

    /// <summary>Right-clicked furniture: its name and Remove.</summary>
    private void ShowFurnitureMenu(FurnitureItem item)
    {
        _furnitureMenu ??= new System.Windows.Forms.ContextMenuStrip();
        foreach (var old in _furnitureMenu.Items.Cast<System.Windows.Forms.ToolStripItem>().ToList())
        {
            old.Dispose();
        }

        _furnitureMenu.Items.Clear();
        var header = new System.Windows.Forms.ToolStripLabel(item.Spec.Name);
        header.Font = new System.Drawing.Font(header.Font, System.Drawing.FontStyle.Bold);
        _furnitureMenu.Items.Add(header);
        if (item.Spec.IsPen)
        {
            _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripLabel("Drop a pet in to keep it safe; drag it out to let it go."));
        }

        _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem($"Remove {item.Spec.Name}", null, (_, _) => _furniture?.Remove(item)));
        _furnitureMenu.Show(System.Windows.Forms.Cursor.Position);
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
            _furniture?.BringToFront();
            BringPetsToFront(); // keep pets above the overlay and the furniture
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
            new DebugView(
                world.Platforms, world.Walls, [.. infos.Select(i => i.Pet)], world.Snapshot.Windows.Count, _furniture?.DebugInfo ?? []));
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

            _furniture?.Reconfigure(config);
            if (_chat is not null)
            {
                _chat.Rules = config.Chat; // the model file and the GPU choice only change on restart
            }
            _tray?.Notify("Settings applied", "Your changes to settings.json are live.");
        }
        else
        {
            ReportProblems("Settings not applied (the previous settings stay in force)", settings.Errors);
        }
    }

    /// <summary>The shared sprites changed: every pet reloads (each pet's own sprites still win).</summary>
    private void OnSpritesChanged()
    {
        var problems = new List<string>();
        foreach (var pet in _pets)
        {
            var (atlas, layers) = SpriteAtlas.Load(_data.SpriteFolders(pet.Id), Animations.All, pet.Variant);
            pet.SetAtlas(atlas);
            problems.AddRange(SpriteProblems(layers, pet.Name));
        }

        if (problems.Count > 0)
        {
            ReportProblems("Some sprites couldn't be used (the built-in art is shown instead)", [.. problems.Distinct()]);
        }
        else if (_pets.Count > 0)
        {
            var custom = _pets[0].CustomAnimations;
            _tray?.Notify("Sprites reloaded", custom == 0
                ? "No custom art found: using the built-in rabbit."
                : $"{AnimationCount(custom)} with custom art.");
        }
    }

    /// <summary>One of a pet's own files changed: traits.json, personality.txt or its sprites folder.</summary>
    private void OnPetChanged(int id, PetFileKind kind)
    {
        var pet = _pets.FirstOrDefault(p => p.Id == id);
        if (pet is null)
        {
            return; // not running (removed, or beyond the cap)
        }

        switch (kind)
        {
            case PetFileKind.Traits:
                OnTraitsChanged(pet);
                break;

            case PetFileKind.Personality:
                if (_data.LoadPersonality(id) is { } text && text != pet.Personality)
                {
                    pet.Personality = text;
                    _tray?.Notify($"{pet.Name}'s personality updated", text);
                }

                break;

            case PetFileKind.Sprites:
                var (atlas, layers) = SpriteAtlas.Load(_data.SpriteFolders(id), Animations.All, pet.Variant);
                pet.SetAtlas(atlas);
                var problems = SpriteProblems(layers, pet.Name);
                if (problems.Count > 0)
                {
                    ReportProblems($"Some of {pet.Name}'s sprites couldn't be used", problems);
                }
                else
                {
                    _tray?.Notify($"{pet.Name}'s sprites reloaded", $"{AnimationCount(atlas.CustomCount)} with custom art.");
                }

                break;
        }
    }

    private void OnTraitsChanged(PetHost pet)
    {
        var loaded = _data.LoadTraits(pet.Id);
        if (loaded is null || loaded.Traits == pet.Traits)
        {
            return; // deleted (the current traits stay in force) or unchanged, such as our own write
        }

        if (loaded.Traits is not { } traits)
        {
            ReportProblems($"{pet.Name}'s traits not applied (the previous ones stay)", loaded.Errors);
            return;
        }

        var oldName = pet.Name;
        pet.SetTraits(traits);
        var renamed = oldName != traits.Name ? $"{oldName} is now called {traits.Name}. " : string.Empty;
        _tray?.Notify($"{traits.Name}'s traits applied", renamed + "Your changes to traits.json are live.");
    }

    /// <summary>Opens the pet's traits.json for editing, writing it first if it's missing.</summary>
    private void EditTraits(PetHost pet)
    {
        var path = _data.TraitsPath(pet.Id);
        var problems = new List<string>();
        if (!File.Exists(path))
        {
            TryWrite(() => _data.WriteTraits(pet.Id, pet.Traits), problems);
        }

        if (problems.Count > 0)
        {
            ReportProblems($"{pet.Name}'s traits.json couldn't be written", problems);
            return;
        }

        DataFolder.Open(path);
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
        _tray?.Notify("Sprite template exported",
            "Copy template.png and atlas.json into the sprites folder (all pets) or a pet's own sprites folder, and paint over them.");
    }

    /// <summary>Problems in a pet's sprite folders, saying whose folder each one is in.</summary>
    private List<string> SpriteProblems(IReadOnlyList<SpriteLayer> layers, string petName) =>
        [.. layers
            .Where(l => l.Spec is { Problems.Count: > 0 })
            .SelectMany(l =>
            {
                var shared = string.Equals(l.Folder, _data.Sprites, StringComparison.OrdinalIgnoreCase);
                return l.Spec!.Problems.Select(p => (shared ? "sprites: " : $"{petName}'s sprites: ") + p);
            })];

    private static string AnimationCount(int count) => $"{count} animation{(count == 1 ? string.Empty : "s")}";

    private static void TryWrite(Action write, List<string> problems)
    {
        try
        {
            write();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add(e.Message);
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

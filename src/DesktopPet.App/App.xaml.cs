using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.App.Breeding;
using DesktopPet.App.Chat;
using DesktopPet.App.Customization;
using DesktopPet.App.Furniture;
using DesktopPet.App.SpriteSheet;
using DesktopPet.App.UI;
using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Animation;
using DesktopPet.Core.Breeding;
using DesktopPet.Core.Death;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Traits;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App;

/// <summary>
/// Composition root. No main window: one window per pet plus a tray icon; exit via the menu.
/// Pets come and go with their folders (pets\n\). Settings, shared sprites, furniture and each pet's traits, personality
/// and own sprites come from the data folder and reload live. The furniture is shared by all pets. A dead pet's folder
/// stays (its save is marked dead) and it has a gravestone instead of a window, until it's revived or deleted.
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
    private BreedingHost? _breeding;
    private Dictionary<string, FurnitureSpec> _furnitureSpecs = new(StringComparer.OrdinalIgnoreCase);
    private System.Windows.Forms.ContextMenuStrip? _furnitureMenu;
    private DispatcherTimer? _overlayTimer;

    /// <summary>Runs before WPF creates any window (Main lives in this class).</summary>
    static App() => PerMonitorDpi = DpiAwareness.EnsurePerMonitorV2();

    /// <summary>Whether Win32 coordinates are physical pixels on every monitor (see <see cref="DpiAwareness"/>).</summary>
    private static bool PerMonitorDpi { get; }

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

        _data.MigrateFromAppData();
        _data.EnsureExists();
        var problems = new List<string>(_data.MigrateLegacySaves());
        problems.AddRange(_data.MigrateTraitsFiles());
        if (!PerMonitorDpi)
        {
            problems.Add("Windows didn't make DesktopPet per-monitor DPI aware, so pets may misjudge windows on monitors"
                + " with a different scale. Start it from DesktopPet.App.exe.");
        }
        var settings = _data.LoadSettings();
        _config = settings.Config ?? PetConfig.Default;
        _pushingEnabled = _config.EnablePushing;
        _desktop = new DesktopSource();

        // Furniture first: the pets share its world, and its windows go behind theirs.
        problems.AddRange(_data.WriteDefaultFurniture());
        _furniture = new FurnitureHost(_config, _desktop, new RoomStore(_data.Room), ShowFurnitureMenu, BringPetsToFront);
        problems.AddRange(LoadFurniture());
        _furniture.Start();

        // Read every pet's traits first, so names rolled for pets without any avoid all the existing ones.
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
                problems.AddRange(loaded.Errors.Select(error => $@"pets\{id}\save.json: {error}"));
            }
        }

        // The first pet arrives at the top centre as always; the others drop in at random spots instead of in a pile.
        foreach (var (id, loaded) in startup)
        {
            problems.AddRange(StartPet(id, loaded, names, dropIn: _pets.Count > 0));
        }

        PlaceGraves();

        _menu = new PetMenu(
            defaultTarget: () => _lastClicked ?? _pets.FirstOrDefault(),
            canAdd: () => RoomForPet,
            canRemove: () => _pets.Count > 1,
            onAdd: AddPet,
            onRemove: pet => RemovePet((PetHost)pet),
            petFiles: (pet => EditTraits((PetHost)pet), pet => DataFolder.Open(_data.PetFolder(((PetHost)pet).Id))),
            onFixed: (pet, isFixed) => SetFixed((PetHost)pet, isFixed),
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

        _breeding = new BreedingHost(() => _pets, _furniture, LayEgg, Hatch, s => _chat?.Say(s.PetId, s.Text)) { Rules = _config.Breeding };
        _breeding.Start();

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
        _breeding?.Dispose();
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
    /// Starts pet <paramref name="id"/> from its folder: its save (or fresh), its traits (rolled if its save has none,
    /// and saved with it; average ones while they're broken), its personality (written from the traits if missing)
    /// and its sprites. Returns the problems found, for reporting.
    /// </summary>
    /// <param name="id">The pet's id (its folder name).</param>
    /// <param name="loaded">The Traits in its save.json as read, or null if it has none.</param>
    /// <param name="names">Names of all pets, including ones not started yet; a newly rolled name is added.</param>
    /// <param name="dropIn">Drop in at a random spot rather than at the top centre.</param>
    /// <param name="dropAt">Drop in here instead (a hatchling, where its egg was).</param>
    private List<string> StartPet(int id, TraitsParseResult? loaded, List<string> names, bool dropIn, Vec2? dropAt = null)
    {
        var problems = new List<string>();
        _data.EnsurePetFolder(id);
        var traits = loaded?.Traits;
        if (loaded is null)
        {
            var rolled = TraitRoller.Roll(_config, _traitRandom, names, WornCharacters());
            names.Add(rolled.Name);
            traits = rolled; // saved into save.json below
            TryWrite(() => _data.WriteCharacterSprites(id, rolled.Character!), problems);
        }

        traits ??= PetTraits.Neutral($"Pet {id}"); // its Traits are broken (reported by the caller; the file keeps them)
        var personality = _data.LoadPersonality(id);
        if (personality is null)
        {
            var described = Personality.Describe(traits with { Character = Characters.For(id, traits.Character).Id });
            TryWrite(() => _data.WritePersonality(id, described), problems);
            personality = described;
        }

        var look = new BuiltInLook(Characters.For(id, traits.Character).Id, JacketColors.For(id, traits.Jacket));
        var (atlas, layers) = SpriteAtlas.Load(_data.SpriteFolders(id), Animations.All, look);
        problems.AddRange(SpriteProblems(layers, traits.Name));

        var pet = new PetHost(id, _config, traits, personality, new JsonFileSaveStore(_data.SavePath(id)), atlas, _desktop!, _furniture!.World)
        {
            PushingEnabled = _pushingEnabled,
            FurnitureWindow = _furniture.WindowOf,
            TraitProblems = loaded is { Traits: null } ? string.Join("\n", loaded.Errors) : null,
        };
        pet.BrokenSaveKept += (_, copy) => ReportProblems(
            $"{pet.Name}'s save.json wasn't valid JSON",
            [$"It was saved over to keep the pet going. Your version is in {Path.GetFileName(copy)} next to it."]);
        pet.MenuRequested += (_, _) =>
        {
            _lastClicked = pet;
            _menu?.ShowFor(pet, System.Windows.Forms.Cursor.Position);
        };
        pet.Warned += (_, warning) => OnWarned(pet, warning);
        pet.Died += (_, death) => OnDied(pet, death);

        if (dropAt is { } at)
        {
            pet.DropFrom(at);
        }
        else if (dropIn)
        {
            pet.DropFrom(RandomDropPoint());
        }

        pet.Start();
        pet.Save(); // a new pet exists from now on, even if the app is killed (with its traits, if they were rolled)
        _pets.Add(pet);
        return problems;
    }

    private void AddPet()
    {
        if (_pets.Count + (_furniture?.EggCount ?? 0) >= PetRoster.MaxPets)
        {
            return;
        }

        // A new pet: random numbers, then a few personality traits from the list, which nudge them.
        var id = NewPetId();
        var names = _pets.Select(p => p.Name).ToList();
        var (list, listProblems) = _data.LoadPersonalityTraits();
        var (traits, personality) = PersonalityTraits.Give(TraitRoller.Roll(_config, _traitRandom, names, WornCharacters()), list, _traitRandom);
        var problems = listProblems.Select(p => $"{PersonalityTraits.FileName} (the default list is used): {p}").ToList();
        TryWrite(() => _data.WritePersonality(id, personality), problems);
        TryWrite(() => _data.WriteCharacterSprites(id, traits.Character!), problems); // its own copy, for the user to edit
        names.Add(traits.Name);
        problems.AddRange(StartPet(id, new TraitsParseResult(traits, []), names, dropIn: true)); // saves the traits // shown last, so it's above the debug overlay if that's on
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

    /// <summary>The characters the running pets wear, so a new pet can get one the fewest wear.</summary>
    private List<string> WornCharacters() => [.. _pets.Select(p => p.Character.Id)];

    /// <summary>A free pet id; also skips folders that exist but weren't loaded (beyond the cap), so none gets overwritten.</summary>
    private int NewPetId() => PetRoster.NextId(_pets.Select(p => p.Id).Concat(_data.ExistingPets()));

    // ---- Breeding ----

    /// <summary>Two pets mated: the egg drops in between them, holding the child they'll have.</summary>
    private void LayEgg(Mating mating)
    {
        var a = _pets.FirstOrDefault(p => p.Id == mating.ParentA);
        var b = _pets.FirstOrDefault(p => p.Id == mating.ParentB);
        if (a is null || b is null || _furniture is null)
        {
            return;
        }

        var egg = Inheritance.Egg(
            new Parent(a.Id, a.Traits, _data.HasOwnSprites(a.Id)), new Parent(b.Id, b.Traits, _data.HasOwnSprites(b.Id)),
            _config.Breeding, _traitRandom, DateTimeOffset.UtcNow);
        _furniture.AddEgg(mating.EggFeet, egg);
        BringPetsToFront();
        _tray?.Notify($"{a.Name} and {b.Name} had an egg", $"It hatches {HatchesIn(egg)}.");
    }

    /// <summary>
    /// An egg's time has come: the child gets a free id and name, personality traits from the list, maybe a parent's
    /// sprites, and comes out where the egg was (on its mating cooldown, like its parents).
    /// </summary>
    private void Hatch(FurnitureItem item)
    {
        if (item.Egg is not { } egg || _furniture is null)
        {
            return;
        }

        _furniture.Remove(item);
        var id = NewPetId();
        var names = _pets.Select(p => p.Name).ToList();
        var (list, listProblems) = _data.LoadPersonalityTraits();
        var named = egg.Child with { Name = PetNames.Pick(_traitRandom, names) };
        var (traits, personality) = PersonalityTraits.Give(named, list, _traitRandom);
        var problems = listProblems.Select(p => $"{PersonalityTraits.FileName} (the default list is used): {p}").ToList();
        var now = DateTimeOffset.UtcNow;
        TryWrite(() => _data.WritePersonality(id, personality), problems);
        TryWrite(() => new JsonFileSaveStore(_data.SavePath(id)).Save(
            SaveData.From(PetStats.Fresh, now, lastMated: now) with { BornUtc = now, Traits = traits }, replaceTraits: true), problems);
        if (egg.SpritesFrom is { } parent && _data.HasOwnSprites(parent))
        {
            TryWrite(() => _data.CopySprites(parent, id), problems);
        }
        else
        {
            // The parent it takes after draws its character's built-in art: the child gets its own copy of it.
            TryWrite(() => _data.WriteCharacterSprites(id, Characters.For(id, traits.Character).Id), problems);
        }

        names.Add(traits.Name);
        var feet = item.Feet;
        problems.AddRange(StartPet(id, new TraitsParseResult(traits, []), names, dropIn: false, dropAt: new Vec2(feet.X, feet.Y - 2)));
        if (problems.Count > 0)
        {
            ReportProblems($"{traits.Name}'s files have problems", problems);
        }
        else
        {
            _tray?.Notify($"{egg.ParentNames}'s egg hatched: meet {traits.Name}", personality);
        }
    }

    /// <summary>"in about 40 minutes", "any moment now".</summary>
    private static string HatchesIn(EggInfo egg)
    {
        var minutes = (int)System.Math.Ceiling((egg.HatchAtUtc - DateTimeOffset.UtcNow).TotalMinutes);
        return minutes switch
        {
            <= 1 => "any moment now",
            < 90 => $"in about {minutes} minutes",
            _ => $"in about {System.Math.Round(minutes / 60.0)} hours",
        };
    }

    /// <summary>Fixes a pet (or undoes it) by rewriting the Traits in its save.json: a fixed pet never mates.</summary>
    private void SetFixed(PetHost pet, bool isFixed)
    {
        var old = pet.Traits;
        var problems = new List<string>();
        TryWrite(() => pet.WriteTraits(old with { Fixed = isFixed }), problems);
        if (problems.Count > 0)
        {
            pet.SetTraits(old);
            ReportProblems($"{pet.Name}'s save.json couldn't be written", problems);
            return;
        }

        _tray?.Notify(isFixed ? $"{pet.Name} is fixed" : $"{pet.Name} isn't fixed any more",
            isFixed ? "No babies from now on." : "Babies are possible again.");
    }

    /// <summary>Closes the pet and moves its folder to the Recycle Bin, so it doesn't come back next time.</summary>
    private void RemovePet(PetHost pet)
    {
        if (_pets.Count <= 1 || !_pets.Remove(pet))
        {
            return;
        }

        _chat?.Forget(pet.Id);
        _breeding?.Forget(pet.Id);
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

    // ---- Death ----

    /// <summary>A need went critical or reached 0: a notification, so the user can step in before it's too late.</summary>
    private void OnWarned(PetHost pet, PetWarning warning)
    {
        var name = pet.Name;
        var left = InAbout(warning.HoursLeft);
        var (title, text) = (warning.Kind, warning.Need) switch
        {
            (WarningKind.Critical, Need.Hunger) => ($"{name} is very hungry", $"Feed {name} soon."),
            (WarningKind.Critical, Need.Happiness) => ($"{name} is very unhappy", $"Play with {name}, or give {name} a pat (a click)."),
            (WarningKind.Critical, _) => ($"{name} is worn out", $"Let {name} sleep (Sleep in the menu)."),
            (WarningKind.AtZero, Need.Hunger) => ($"{name} is starving!", $"Without food, {name} will die {left}."),
            (WarningKind.AtZero, Need.Happiness) => ($"{name} is miserable!", $"Without some play, {name} will die {left}."),
            (WarningKind.AtZero, _) => ($"{name} has no energy left!", $"Without sleep, {name} will die {left}."),
            _ => ($"{name} is in a bad way", $"Every need is critical: {name} will die {left} without care."),
        };
        _tray?.Notify(title, text, warning: warning.Kind != WarningKind.Critical);
    }

    /// <summary>"in about 2 hours", "in about 40 minutes", "any moment now".</summary>
    private static string InAbout(double hours)
    {
        var minutes = (int)System.Math.Ceiling(hours * 60);
        return minutes switch
        {
            <= 1 => "any moment now",
            < 90 => $"in about {minutes} minutes",
            _ => $"in about {System.Math.Round(minutes / 60.0)} hours",
        };
    }

    /// <summary>A pet died of neglect: its window goes, its save is marked dead, and a gravestone stands where it was.</summary>
    private void OnDied(PetHost pet, PetDeath death)
    {
        if (!_pets.Remove(pet))
        {
            return;
        }

        _chat?.Forget(pet.Id);
        _breeding?.Forget(pet.Id);
        if (_lastClicked == pet)
        {
            _lastClicked = null;
        }

        var feet = pet.Feet;
        pet.Close(save: true); // the save says it's dead, so it isn't started again
        var grave = new GraveInfo { PetId = pet.Id, Name = pet.Name, BornUtc = pet.BornUtc, DiedUtc = death.AtUtc, Cause = death.Cause };
        _furniture?.AddGrave(new Vec2(feet.X, feet.Y - 2), grave);
        BringPetsToFront();
        _tray?.Notify($"{pet.Name} has died", $"{grave.Story} Right-click the gravestone to bring {pet.Name} back.", warning: true);
    }

    /// <summary>
    /// At startup: every dead pet gets one gravestone (where room.json had it, else dropped in), with its details read
    /// from its folder. Gravestones of pets that aren't dead any more, or whose folder is gone, are removed.
    /// </summary>
    private void PlaceGraves()
    {
        var dead = _data.DeadPets().ToDictionary(d => d.Id, d => d.Save);
        var placed = new HashSet<int>();
        foreach (var item in _furniture!.World.GraveItems.ToList())
        {
            var id = item.Grave!.PetId;
            if (!dead.TryGetValue(id, out var save) || !placed.Add(id))
            {
                _furniture.Remove(item);
                continue;
            }

            _furniture.World.SetGrave(item, GraveFor(id, save, item.Grave.Name));
        }

        foreach (var (id, save) in dead.Where(d => !placed.Contains(d.Key)))
        {
            _furniture.AddGrave(RandomDropPoint(), GraveFor(id, save, $"Pet {id}"));
        }
    }

    /// <summary>A dead pet's gravestone details: its name from its traits (else <paramref name="fallbackName"/>), its dates from its save.</summary>
    private GraveInfo GraveFor(int id, SaveData save, string fallbackName)
    {
        var name = _data.LoadTraits(id)?.Traits?.Name ?? fallbackName;
        var grave = GraveInfo.From(id, name, save)!;
        if (grave.BornUtc is null && Directory.Exists(_data.PetFolder(id)))
        {
            grave = grave with { BornUtc = Directory.GetCreationTimeUtc(_data.PetFolder(id)) };
        }

        return grave;
    }

    /// <summary>Pets plus eggs are below the cap (gravestones don't count).</summary>
    private bool RoomForPet => _pets.Count + (_furniture?.EggCount ?? 0) < PetRoster.MaxPets;

    /// <summary>
    /// Brings a dead pet back where its gravestone was: the same folder (traits, personality, art), its save no longer
    /// dead and every need at a safe level.
    /// </summary>
    private void Revive(FurnitureItem item)
    {
        if (item.Grave is not { } grave || _furniture is null || !RoomForPet)
        {
            return;
        }

        var id = grave.PetId;
        var store = new JsonFileSaveStore(_data.SavePath(id));
        var problems = new List<string>();
        var revived = (store.Load() ?? new SaveData()).Revived(_config.Death, DateTimeOffset.UtcNow);
        TryWrite(() => store.Save(revived), problems);
        if (problems.Count > 0)
        {
            ReportProblems($"{grave.Name} couldn't be brought back", problems);
            return;
        }

        var feet = item.Feet;
        _furniture.Remove(item);
        var loaded = _data.LoadTraits(id);
        if (loaded is { Traits: null })
        {
            problems.AddRange(loaded.Errors.Select(error => $@"pets\{id}\save.json: {error}"));
        }

        var names = _pets.Select(p => p.Name).ToList();
        problems.AddRange(StartPet(id, loaded, names, dropIn: false, dropAt: new Vec2(feet.X, feet.Y - 2)));
        var pet = _pets[^1];
        if (problems.Count > 0)
        {
            ReportProblems($"{pet.Name} is back, but some files have problems", problems);
        }
        else
        {
            _tray?.Notify($"{pet.Name} is back!", $"Take good care of {pet.Name} this time.");
        }
    }

    /// <summary>Deletes a dead pet for good: its folder goes to the Recycle Bin, then its gravestone goes.</summary>
    private void DeleteGrave(FurnitureItem item)
    {
        if (item.Grave is not { } grave || _furniture is null)
        {
            return;
        }

        try
        {
            _data.RecyclePetFolder(grave.PetId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            ReportProblems($"{grave.Name}'s folder couldn't be removed (the gravestone stays)", [ex.Message]);
            return;
        }

        _furniture.Remove(item);
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
        var size = _config.SizePxAt(primary.Scale);
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
            if (string.Equals(kind, Eggs.Kind, StringComparison.OrdinalIgnoreCase) || string.Equals(kind, Graves.Kind, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($@"furniture\{kind}.json: ""{kind}"" is built in (the egg or the gravestone); rename the file to use it as furniture");
                continue;
            }

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

        art[Eggs.Kind] = FurnitureArt.BuiltIn(Eggs.Spec) ?? [];
        art[Graves.Kind] = FurnitureArt.BuiltIn(Graves.Spec) ?? [];
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

    /// <summary>Right-clicked furniture: its name and Remove. A gravestone: whose it is, Revive and Delete.</summary>
    private void ShowFurnitureMenu(FurnitureItem item)
    {
        _furnitureMenu ??= new System.Windows.Forms.ContextMenuStrip();
        foreach (var old in _furnitureMenu.Items.Cast<System.Windows.Forms.ToolStripItem>().ToList())
        {
            old.Dispose();
        }

        _furnitureMenu.Items.Clear();
        var header = new System.Windows.Forms.ToolStripLabel(item.Grave is { } dead ? $"{dead.Name}'s gravestone" : item.Spec.Name);
        header.Font = new System.Drawing.Font(header.Font, System.Drawing.FontStyle.Bold);
        _furnitureMenu.Items.Add(header);
        if (item.Grave is { } grave)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var died = GraveInfo.Date(grave.DiedUtc, culture);
            var dates = grave.BornUtc is { } born ? $"{GraveInfo.Date(born, culture)} – {died}" : $"Died {died}";
            _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripLabel($"{dates}. {grave.Story}"));
            _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            var room = RoomForPet;
            _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(
                room ? $"Revive {grave.Name}" : $"Revive {grave.Name} (no room: {PetRoster.MaxPets} pets and eggs already)", null, (_, _) => Revive(item))
            {
                Enabled = room,
            });
            _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(
                $"Delete {grave.Name} for good (folder to the Recycle Bin)", null, (_, _) => DeleteGrave(item)));
            _furnitureMenu.Show(System.Windows.Forms.Cursor.Position);
            return;
        }

        if (item.Egg is { } laid)
        {
            _furnitureMenu.Items.Add(new System.Windows.Forms.ToolStripLabel($"Laid by {laid.ParentNames}. It hatches {HatchesIn(laid)}."));
        }

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

            if (_breeding is not null)
            {
                _breeding.Rules = config.Breeding;
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
            var (atlas, layers) = SpriteAtlas.Load(_data.SpriteFolders(pet.Id), Animations.All, pet.Look);
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
                ? "No custom art found: using the built-in characters."
                : $"{AnimationCount(custom)} with custom art.");
        }
    }

    /// <summary>One of a pet's own files changed: save.json (its traits), personality.txt or its sprites folder.</summary>
    private void OnPetChanged(int id, PetFileKind kind)
    {
        var pet = _pets.FirstOrDefault(p => p.Id == id);
        if (pet is null)
        {
            return; // not running (removed, or beyond the cap)
        }

        switch (kind)
        {
            case PetFileKind.Save:
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
                var (atlas, layers) = SpriteAtlas.Load(_data.SpriteFolders(id), Animations.All, pet.Look);
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
            // Deleted (the current traits stay in force, and the next save writes them back) or unchanged, such as an autosave.
            pet.TraitProblems = loaded is null ? pet.TraitProblems : null;
            return;
        }

        if (loaded.Traits is not { } traits)
        {
            // Autosaves keep the broken Traits as they are, so the same problems come back each minute: report them once.
            var problems = string.Join("\n", loaded.Errors);
            if (problems != pet.TraitProblems)
            {
                pet.TraitProblems = problems;
                ReportProblems($"{pet.Name}'s traits not applied (the previous ones stay)", loaded.Errors);
            }

            return;
        }

        pet.TraitProblems = null;

        var oldName = pet.Name;
        var oldLook = pet.Look;
        pet.SetTraits(traits);
        if (pet.Look != oldLook)
        {
            var (atlas, _) = SpriteAtlas.Load(_data.SpriteFolders(pet.Id), Animations.All, pet.Look);
            pet.SetAtlas(atlas);
        }

        var renamed = oldName != traits.Name ? $"{oldName} is now called {traits.Name}. " : string.Empty;
        _tray?.Notify($"{traits.Name}'s traits applied", renamed + "Your changes to save.json are live.");
    }

    /// <summary>Opens the pet's save.json (its Traits are the part to edit), writing it first if it's missing.</summary>
    private void EditTraits(PetHost pet)
    {
        var path = _data.SavePath(pet.Id);
        var problems = new List<string>();
        if (!File.Exists(path))
        {
            TryWrite(() => pet.WriteTraits(pet.Traits), problems);
        }

        if (problems.Count > 0)
        {
            ReportProblems($"{pet.Name}'s save.json couldn't be written", problems);
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

using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Traits;

namespace DesktopPet.App.UI;

/// <summary>
/// The pets' menu, shared by the tray icon and right-clicking a pet. It's always about one pet, its target: a header
/// naming it with its mood, live stat bars, an About submenu (personality and traits, read-only) and care actions for
/// it. Then app-wide items: furniture (placed next to the target), adding and removing pets,
/// pushing, customization, the debug overlay, Exit. With no pets at all (every one dead) only the app-wide items show.
/// Uses WinForms' ContextMenuStrip, which closes properly on outside clicks even though the pets' windows never activate.
/// </summary>
internal sealed class PetMenu : IDisposable
{
    private const int BarLength = 10;
    private const int WrapColumn = 52;
    private const int MaxPersonalityLines = 8;

    private readonly Func<IPetControls?> _defaultTarget;
    private readonly Func<bool> _canAdd;
    private readonly Func<bool> _canRemove;
    private readonly ToolStripLabel _header;
    private readonly ToolStripLabel _hunger;
    private readonly ToolStripLabel _happiness;
    private readonly ToolStripLabel _energy;
    private readonly ToolStripMenuItem _about;
    private readonly ToolStripLabel _personality;
    private readonly ToolStripLabel _neediness;
    private readonly ToolStripLabel _behavior;
    private readonly ToolStripLabel _chat;
    private readonly ToolStripLabel _family;
    private readonly ToolStripMenuItem _fixed;
    private readonly ToolStripMenuItem _editTraits;
    private readonly ToolStripMenuItem _openFolder;
    private readonly ToolStripMenuItem _sleep;
    private readonly ToolStripMenuItem _add;
    private readonly ToolStripMenuItem _remove;
    private readonly ToolStripMenuItem _furnitureMenu;
    private readonly FurnitureActions _furniture;
    private readonly ToolStripItem[] _petItems;
    private IPetControls? _target;
    private bool _openingForPet;

    /// <param name="defaultTarget">The pet to show when opened from the tray (the last one right-clicked, or the first); null if there are none.</param>
    /// <param name="canAdd">Whether another pet may be added (below the limit).</param>
    /// <param name="canRemove">Whether a pet may be removed (not the last one).</param>
    /// <param name="onAdd">"Add a pet" chosen.</param>
    /// <param name="onRemove">"Remove this pet" chosen for the target.</param>
    /// <param name="petFiles">About submenu actions for the target: edit its traits.json, open its folder.</param>
    /// <param name="onFixed">"Fixed" toggled for the target: it can't have babies (saved in its traits.json).</param>
    /// <param name="furniture">Furniture submenu: the kinds, adding one beside the target, removing all, the folder.</param>
    /// <param name="pushingEnabled">Initial state of "Let it push windows".</param>
    /// <param name="onPushing">"Let it push windows" toggled (applies to all pets).</param>
    /// <param name="chat">Initial state of "Let them talk" (speech bubbles), and what to do when it's toggled.</param>
    /// <param name="notes">Initial state of "Let them leave notes" (sticky notes, a separate behavior), and what to do when it's toggled.</param>
    /// <param name="reading">Initial state of "Let them read the screen" (talking and writing about what's on windows), and what to do when it's toggled.</param>
    /// <param name="customize">Customize submenu actions: edit settings, open data folder, export sprite template.</param>
    /// <param name="onDebugOverlay">Debug overlay toggled.</param>
    /// <param name="onExit">Exit chosen.</param>
    public PetMenu(
        Func<IPetControls?> defaultTarget, Func<bool> canAdd, Func<bool> canRemove, Action onAdd, Action<IPetControls> onRemove,
        (Action<IPetControls> EditTraits, Action<IPetControls> OpenFolder) petFiles, Action<IPetControls, bool> onFixed, FurnitureActions furniture, bool pushingEnabled, Action<bool> onPushing, (bool Enabled, Action<bool> OnChanged) chat,
        (bool Enabled, Action<bool> OnChanged) notes, (bool Enabled, Action<bool> OnChanged) reading,
        (Action EditSettings, Action OpenDataFolder, Action ExportSpriteTemplate) customize,
        Action<bool> onDebugOverlay, Action onExit)
    {
        _defaultTarget = defaultTarget;
        _canAdd = canAdd;
        _canRemove = canRemove;
        _furniture = furniture;
        _furnitureMenu = new ToolStripMenuItem("Furniture");

        var mono = new Font("Consolas", 9.5f);
        _header = new ToolStripLabel { Font = new Font(SystemFonts.MenuFont ?? mono, FontStyle.Bold) };
        _hunger = new ToolStripLabel { Font = mono };
        _happiness = new ToolStripLabel { Font = mono };
        _energy = new ToolStripLabel { Font = mono };

        _personality = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _neediness = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _behavior = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _chat = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _family = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _editTraits = new ToolStripMenuItem("Edit traits…", null, (_, _) => ForTarget(petFiles.EditTraits));
        _openFolder = new ToolStripMenuItem("Open its folder", null, (_, _) => ForTarget(petFiles.OpenFolder));
        _about = new ToolStripMenuItem("About");
        _about.DropDownItems.AddRange(
        [
            _personality,
            new ToolStripSeparator(),
            _neediness,
            _behavior,
            _chat,
            _family,
            new ToolStripSeparator(),
            _editTraits,
            _openFolder,
        ]);

        // Not CheckOnClick: the click handler decides from the pet's traits, so the tick always shows the file's state.
        _fixed = new ToolStripMenuItem("Fixed (no babies)", null, (_, _) => ForTarget(pet => onFixed(pet, !pet.Traits.Fixed)));

        var feed = new ToolStripMenuItem("Feed", null, (_, _) => ForTarget(pet => pet.Apply(PetAction.Feed)));
        var play = new ToolStripMenuItem("Play", null, (_, _) => ForTarget(pet => pet.Apply(PetAction.Play)));
        _sleep = new ToolStripMenuItem("Sleep", null, (_, _) => ForTarget(pet => pet.Apply(pet.IsAsleep ? PetAction.WakeUp : PetAction.Sleep)));

        _add = new ToolStripMenuItem("Add a pet", null, (_, _) => onAdd());
        _remove = new ToolStripMenuItem("Remove this pet", null, (_, _) => ForTarget(onRemove));

        var pushing = new ToolStripMenuItem("Let it push windows") { CheckOnClick = true, Checked = pushingEnabled };
        pushing.CheckedChanged += (_, _) => onPushing(pushing.Checked);

        var talking = new ToolStripMenuItem("Let them talk") { CheckOnClick = true, Checked = chat.Enabled };
        talking.CheckedChanged += (_, _) => chat.OnChanged(talking.Checked);
        var leavingNotes = new ToolStripMenuItem("Let them leave notes") { CheckOnClick = true, Checked = notes.Enabled };
        leavingNotes.CheckedChanged += (_, _) => notes.OnChanged(leavingNotes.Checked);
        var readingScreen = new ToolStripMenuItem("Let them read the screen") { CheckOnClick = true, Checked = reading.Enabled };
        readingScreen.CheckedChanged += (_, _) => reading.OnChanged(readingScreen.Checked);

        var debugOverlay = new ToolStripMenuItem("Debug overlay") { CheckOnClick = true };
        debugOverlay.CheckedChanged += (_, _) => onDebugOverlay(debugOverlay.Checked);

        var customizeMenu = new ToolStripMenuItem("Customize");
        customizeMenu.DropDownItems.AddRange(
        [
            new ToolStripMenuItem("Edit settings…", null, (_, _) => customize.EditSettings()),
            new ToolStripMenuItem("Open data folder", null, (_, _) => customize.OpenDataFolder()),
            new ToolStripMenuItem("Export sprite template", null, (_, _) => customize.ExportSpriteTemplate()),
        ]);

        var careSeparator = new ToolStripSeparator();
        _petItems = [_hunger, _happiness, _energy, _about, careSeparator, feed, play, _sleep, _fixed, _remove];
        Strip = new ContextMenuStrip();
        Strip.Items.AddRange(
        [
            _header, _hunger, _happiness, _energy, _about,
            careSeparator,
            feed, play, _sleep, _fixed,
            new ToolStripSeparator(),
            _furnitureMenu,
            _add, _remove,
            new ToolStripSeparator(),
            pushing,
            talking,
            leavingNotes,
            readingScreen,
            customizeMenu,
            debugOverlay,
            new ToolStripMenuItem("Exit", null, (_, _) => onExit()),
        ]);
        Strip.Opening += (_, _) =>
        {
            // Opened from the tray (not ShowFor): use the default pet. Decided here, at opening, rather than reset on
            // close, because WinForms may close the menu before running the clicked item's handler.
            if (!_openingForPet)
            {
                _target = null;
            }

            _openingForPet = false;
            Refresh();
        };
    }

    public ContextMenuStrip Strip { get; }

    private IPetControls? Target => _target ?? _defaultTarget();

    /// <summary>Opens the menu for <paramref name="pet"/> at a screen position (physical pixels).</summary>
    public void ShowFor(IPetControls pet, Point screen)
    {
        _target = pet;
        _openingForPet = true;
        Strip.Show(screen);
    }

    public void Dispose() => Strip.Dispose();

    private void ForTarget(Action<IPetControls> action)
    {
        if (Target is { } pet)
        {
            action(pet);
        }
    }

    private void Refresh()
    {
        var pet = Target;
        foreach (var item in _petItems)
        {
            item.Visible = pet is not null;
        }

        _add.Enabled = _canAdd();
        if (pet is null)
        {
            _header.Text = "No pets: add one, or revive one at its gravestone";
            RefreshFurniture(null);
            return;
        }

        var s = pet.Stats;
        _header.Text = $"{pet.Name} · {Mood(s, pet.Rules)}";
        _hunger.Text = Line("Fed", s.Hunger, pet.Rules);
        _happiness.Text = Line("Happy", s.Happiness, pet.Rules);
        _energy.Text = Line("Energy", s.Energy, pet.Rules);
        _sleep.Text = pet.IsAsleep ? "Wake up" : "Sleep";
        _about.Text = $"About {pet.Name}";
        _personality.Text = Wrap(pet.Personality.Length > 0 ? pet.Personality : "(no personality.txt)");
        var n = pet.Traits.Neediness;
        var b = pet.Traits.Behavior;
        _neediness.Text = $"Neediness: hunger {X(n.Hunger)}, happiness {X(n.Happiness)}, energy {X(n.Energy)}";
        _behavior.Text = Wrap(
            $"Likes: idling {X(b.Idle)}, walking {X(b.Walk)}, sitting {X(b.Sit)}, napping {X(b.Sleep)}, " +
            $"climbing {X(b.Climb)}, pushing windows {X(b.Push)}");
        var c = pet.Traits.Chat;
        _chat.Text = Wrap($"Talks: about its needs {X(c.Needy)}, with friends {X(c.Banter)}, jokes {X(c.Jokes)}. Leaves sticky notes {X(c.Notes)}. Curious about what's on screen {X(c.Curiosity)}");
        var t = pet.Traits;
        var parents = t.Parents is { Count: > 0 } p ? $"Child of {string.Join(" and ", p)}. " : string.Empty;
        _family.Text = Wrap(parents + (t.Fixed ? "Fixed: it won't have babies." : $"Keen on mating {X(b.Mating)}."));
        _fixed.Checked = t.Fixed;
        _fixed.Text = $"{pet.Name} is fixed (no babies)";
        RefreshFurniture(pet);
        _remove.Enabled = _canRemove();
        _remove.Text = $"Remove {pet.Name}";
    }

    /// <summary>The Furniture submenu: one "Add" per kind (it drops in beside the pet), then remove all, then the folder.</summary>
    private void RefreshFurniture(IPetControls? pet)
    {
        var items = _furnitureMenu.DropDownItems;
        foreach (var old in items.Cast<ToolStripItem>().ToList())
        {
            old.Dispose();
        }

        items.Clear();
        var canAdd = _furniture.CanAdd() && pet is not null; // it's placed beside the pet
        foreach (var (kind, name) in _furniture.Kinds())
        {
            items.Add(new ToolStripMenuItem($"Add {name}", null, (_, _) =>
            {
                if (pet is not null)
                {
                    _furniture.Add(pet, kind);
                }
            }) { Enabled = canAdd });
        }

        if (items.Count == 0)
        {
            items.Add(new ToolStripLabel("(no furniture defined)"));
        }

        items.Add(new ToolStripSeparator());
        items.Add(new ToolStripMenuItem("Remove all furniture", null, (_, _) => _furniture.RemoveAll()) { Enabled = _furniture.Count() > 0 });
        items.Add(new ToolStripMenuItem("Open furniture folder", null, (_, _) => _furniture.OpenFolder()));
    }

    /// <summary>A multiplier as "×1.25".</summary>
    private static string X(double multiplier) => $"×{multiplier:0.##}";

    /// <summary>Word-wraps for a menu label (labels don't wrap by themselves), at most a few lines.</summary>
    private static string Wrap(string text)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > WrapColumn)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            line.Append(line.Length > 0 ? " " : string.Empty).Append(word);
        }

        lines.Add(line.ToString());
        if (lines.Count > MaxPersonalityLines)
        {
            lines = [.. lines.Take(MaxPersonalityLines)];
            lines[^1] += " …";
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Mood(PetStats s, StatRules rules)
    {
        if (s.Lowest < rules.Critical)
        {
            return "needs care!";
        }

        if (s.Hunger < rules.Sad)
        {
            return "hungry";
        }

        if (s.Energy < rules.Tired * 0.6)
        {
            return "sleepy";
        }

        return s.Happiness < rules.Sad ? "sad" : s.Lowest > 70 ? "happy" : "content";
    }

    private static string Line(string label, double value, StatRules rules)
    {
        var filled = (int)System.Math.Round(value / PetStats.Max * BarLength);
        var bar = new string('█', filled) + new string('░', BarLength - filled); // full and light shade blocks
        var warning = value < rules.Critical ? " !" : string.Empty;
        return $"{label,-7}{bar} {value,3:0}%{warning}";
    }
}

/// <summary>What the Furniture submenu does.</summary>
/// <param name="Kinds">The kinds that can be placed (kind, display name), in menu order.</param>
/// <param name="CanAdd">Whether there's room for another piece.</param>
/// <param name="Count">How many pieces there are.</param>
/// <param name="Add">Place one of a kind beside a pet.</param>
/// <param name="RemoveAll">Remove every piece.</param>
/// <param name="OpenFolder">Open the furniture folder.</param>
internal sealed record FurnitureActions(
    Func<IReadOnlyList<(string Kind, string Name)>> Kinds, Func<bool> CanAdd, Func<int> Count, Action<IPetControls, string> Add,
    Action RemoveAll, Action OpenFolder);

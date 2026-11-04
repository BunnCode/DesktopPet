using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Traits;

namespace DesktopPet.App.UI;

/// <summary>
/// The pets' menu, shared by the tray icon and right-clicking a pet. It's always about one pet, its target: a header
/// naming it with its mood, live stat bars, an About submenu (personality and traits, read-only) and care actions for
/// it. Then app-wide items: adding and removing pets,
/// pushing, customization, the debug overlay, Exit. Uses WinForms' ContextMenuStrip, which closes properly on outside
/// clicks even though the pets' windows never activate.
/// </summary>
internal sealed class PetMenu : IDisposable
{
    private const int BarLength = 10;
    private const int WrapColumn = 52;
    private const int MaxPersonalityLines = 8;

    private readonly Func<IPetControls> _defaultTarget;
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
    private readonly ToolStripMenuItem _editTraits;
    private readonly ToolStripMenuItem _openFolder;
    private readonly ToolStripMenuItem _sleep;
    private readonly ToolStripMenuItem _add;
    private readonly ToolStripMenuItem _remove;
    private IPetControls? _target;
    private bool _openingForPet;

    /// <param name="defaultTarget">The pet to show when opened from the tray (the last one right-clicked, or the first).</param>
    /// <param name="canAdd">Whether another pet may be added (below the limit).</param>
    /// <param name="canRemove">Whether a pet may be removed (not the last one).</param>
    /// <param name="onAdd">"Add a pet" chosen.</param>
    /// <param name="onRemove">"Remove this pet" chosen for the target.</param>
    /// <param name="petFiles">About submenu actions for the target: edit its traits.json, open its folder.</param>
    /// <param name="pushingEnabled">Initial state of "Let it push windows".</param>
    /// <param name="onPushing">"Let it push windows" toggled (applies to all pets).</param>
    /// <param name="customize">Customize submenu actions: edit settings, open data folder, export sprite template.</param>
    /// <param name="onDebugOverlay">Debug overlay toggled.</param>
    /// <param name="onExit">Exit chosen.</param>
    public PetMenu(
        Func<IPetControls> defaultTarget, Func<bool> canAdd, Func<bool> canRemove, Action onAdd, Action<IPetControls> onRemove,
        (Action<IPetControls> EditTraits, Action<IPetControls> OpenFolder) petFiles, bool pushingEnabled, Action<bool> onPushing, (Action EditSettings, Action OpenDataFolder, Action ExportSpriteTemplate) customize,
        Action<bool> onDebugOverlay, Action onExit)
    {
        _defaultTarget = defaultTarget;
        _canAdd = canAdd;
        _canRemove = canRemove;

        var mono = new Font("Consolas", 9.5f);
        _header = new ToolStripLabel { Font = new Font(SystemFonts.MenuFont ?? mono, FontStyle.Bold) };
        _hunger = new ToolStripLabel { Font = mono };
        _happiness = new ToolStripLabel { Font = mono };
        _energy = new ToolStripLabel { Font = mono };

        _personality = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _neediness = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _behavior = new ToolStripLabel { TextAlign = ContentAlignment.MiddleLeft };
        _editTraits = new ToolStripMenuItem("Edit traits…", null, (_, _) => petFiles.EditTraits(Target));
        _openFolder = new ToolStripMenuItem("Open its folder", null, (_, _) => petFiles.OpenFolder(Target));
        _about = new ToolStripMenuItem("About");
        _about.DropDownItems.AddRange(
        [
            _personality,
            new ToolStripSeparator(),
            _neediness,
            _behavior,
            new ToolStripSeparator(),
            _editTraits,
            _openFolder,
        ]);

        var feed = new ToolStripMenuItem("Feed", null, (_, _) => Target.Apply(PetAction.Feed));
        var play = new ToolStripMenuItem("Play", null, (_, _) => Target.Apply(PetAction.Play));
        _sleep = new ToolStripMenuItem("Sleep", null, (_, _) => Target.Apply(Target.IsAsleep ? PetAction.WakeUp : PetAction.Sleep));

        _add = new ToolStripMenuItem("Add a pet", null, (_, _) => onAdd());
        _remove = new ToolStripMenuItem("Remove this pet", null, (_, _) => onRemove(Target));

        var pushing = new ToolStripMenuItem("Let it push windows") { CheckOnClick = true, Checked = pushingEnabled };
        pushing.CheckedChanged += (_, _) => onPushing(pushing.Checked);

        var debugOverlay = new ToolStripMenuItem("Debug overlay") { CheckOnClick = true };
        debugOverlay.CheckedChanged += (_, _) => onDebugOverlay(debugOverlay.Checked);

        var customizeMenu = new ToolStripMenuItem("Customize");
        customizeMenu.DropDownItems.AddRange(
        [
            new ToolStripMenuItem("Edit settings…", null, (_, _) => customize.EditSettings()),
            new ToolStripMenuItem("Open data folder", null, (_, _) => customize.OpenDataFolder()),
            new ToolStripMenuItem("Export sprite template", null, (_, _) => customize.ExportSpriteTemplate()),
        ]);

        Strip = new ContextMenuStrip();
        Strip.Items.AddRange(
        [
            _header, _hunger, _happiness, _energy, _about,
            new ToolStripSeparator(),
            feed, play, _sleep,
            new ToolStripSeparator(),
            _add, _remove,
            new ToolStripSeparator(),
            pushing,
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

    private IPetControls Target => _target ?? _defaultTarget();

    /// <summary>Opens the menu for <paramref name="pet"/> at a screen position (physical pixels).</summary>
    public void ShowFor(IPetControls pet, Point screen)
    {
        _target = pet;
        _openingForPet = true;
        Strip.Show(screen);
    }

    public void Dispose() => Strip.Dispose();

    private void Refresh()
    {
        var pet = Target;
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
        _add.Enabled = _canAdd();
        _remove.Enabled = _canRemove();
        _remove.Text = $"Remove {pet.Name}";
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

using System.Drawing;
using System.Windows.Forms;
using DesktopPet.Core.Stats;

namespace DesktopPet.App.UI;

/// <summary>
/// The pet's menu, shared by the tray icon and right-clicking the pet: a mood line, live stat bars,
/// care actions, then app options. Uses WinForms' ContextMenuStrip, which closes properly on outside clicks
/// even though the pet's window never activates.
/// </summary>
internal sealed class PetMenu : IDisposable
{
    private const int BarLength = 10;

    private readonly Func<PetStats> _stats;
    private readonly Func<bool> _isAsleep;
    private readonly StatRules _rules;
    private readonly ToolStripLabel _mood;
    private readonly ToolStripLabel _hunger;
    private readonly ToolStripLabel _happiness;
    private readonly ToolStripLabel _energy;
    private readonly ToolStripMenuItem _sleep;

    /// <param name="stats">Current stats, read when the menu opens.</param>
    /// <param name="isAsleep">Whether the pet is asleep (the Sleep item becomes Wake up).</param>
    /// <param name="rules">Thresholds for mood text.</param>
    /// <param name="act">Care action chosen.</param>
    /// <param name="onDebugOverlay">Debug overlay toggled.</param>
    /// <param name="onExit">Exit chosen.</param>
    public PetMenu(
        Func<PetStats> stats, Func<bool> isAsleep, StatRules rules, Action<PetAction> act, Action<bool> onDebugOverlay, Action onExit)
    {
        _stats = stats;
        _isAsleep = isAsleep;
        _rules = rules;

        var mono = new Font("Consolas", 9.5f);
        _mood = new ToolStripLabel { Font = new Font(SystemFonts.MenuFont ?? mono, FontStyle.Bold) };
        _hunger = new ToolStripLabel { Font = mono };
        _happiness = new ToolStripLabel { Font = mono };
        _energy = new ToolStripLabel { Font = mono };

        var feed = new ToolStripMenuItem("Feed", null, (_, _) => act(PetAction.Feed));
        var play = new ToolStripMenuItem("Play", null, (_, _) => act(PetAction.Play));
        _sleep = new ToolStripMenuItem("Sleep", null, (_, _) => act(_isAsleep() ? PetAction.WakeUp : PetAction.Sleep));

        var debugOverlay = new ToolStripMenuItem("Debug overlay") { CheckOnClick = true };
        debugOverlay.CheckedChanged += (_, _) => onDebugOverlay(debugOverlay.Checked);

        Strip = new ContextMenuStrip();
        Strip.Items.AddRange(
        [
            _mood, _hunger, _happiness, _energy,
            new ToolStripSeparator(),
            feed, play, _sleep,
            new ToolStripSeparator(),
            debugOverlay,
            new ToolStripMenuItem("Exit", null, (_, _) => onExit()),
        ]);
        Strip.Opening += (_, _) => Refresh();
    }

    public ContextMenuStrip Strip { get; }

    /// <summary>Opens the menu at a screen position (physical pixels).</summary>
    public void ShowAt(Point screen) => Strip.Show(screen);

    public void Dispose() => Strip.Dispose();

    private void Refresh()
    {
        var s = _stats();
        _mood.Text = "DesktopPet · " + Mood(s);
        _hunger.Text = Line("Fed", s.Hunger);
        _happiness.Text = Line("Happy", s.Happiness);
        _energy.Text = Line("Energy", s.Energy);
        _sleep.Text = _isAsleep() ? "Wake up" : "Sleep";
    }

    private string Mood(PetStats s)
    {
        if (s.Lowest < _rules.Critical)
        {
            return "needs care!";
        }

        if (s.Hunger < _rules.Sad)
        {
            return "hungry";
        }

        if (s.Energy < _rules.Tired * 0.6)
        {
            return "sleepy";
        }

        return s.Happiness < _rules.Sad ? "sad" : s.Lowest > 70 ? "happy" : "content";
    }

    private string Line(string label, double value)
    {
        var filled = (int)System.Math.Round(value / PetStats.Max * BarLength);
        var bar = new string('█', filled) + new string('░', BarLength - filled);
        var warning = value < _rules.Critical ? " !" : string.Empty;
        return $"{label,-7}{bar} {value,3:0}%{warning}";
    }
}

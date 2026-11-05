using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using Deskling.Chat;
using Deskling.Core.Abstractions;
using Deskling.Core.Chat;
using Deskling.Core.Math;
using Deskling.Core.World;
using Deskling.Platform.Windows;

namespace Deskling.App.Chat;

/// <summary>
/// Glue for the two behaviors that use the language model: loads the shared model in the background, ticks Core's
/// <see cref="ChatDirector"/> every display frame with the pets as they are, and shows the results: spoken lines as
/// speech bubbles (one per pet), notes as sticky notes on windows.
/// </summary>
internal sealed class ChatHost : IDisposable
{
    private static readonly TimeSpan NoteInterval = TimeSpan.FromMilliseconds(100);

    private readonly Func<IReadOnlyList<PetHost>> _pets;
    private readonly DesktopSource _desktop;
    private readonly Action<string, string, bool> _notify;
    private readonly ChatDirector _director = new(new SeededRandom(Environment.TickCount));
    private readonly Dictionary<int, SpeechBubble> _bubbles = [];
    private readonly List<StickyNote> _notes = [];

    /// <summary>Pets with a bubble up, oldest line first: newer bubbles stack above older ones.</summary>
    private readonly List<int> _spoke = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _sinceNotes = Stopwatch.StartNew();
    private readonly ScreenReader _reader;
    private LlamaChatModel? _model;
    private TimeSpan _last;
    private bool _enabled;
    private bool _disposed;

    /// <param name="pets">The running pets.</param>
    /// <param name="desktop">The desktop, for keeping bubbles on screen.</param>
    /// <param name="notify">Shows a tray notification (title, text, warning).</param>
    public ChatHost(Func<IReadOnlyList<PetHost>> pets, DesktopSource desktop, Action<string, string, bool> notify)
    {
        _pets = pets;
        _desktop = desktop;
        _notify = notify;
        _reader = new ScreenReader(() => Rules, OwnWindows, notify);
    }

    public ChatRules Rules { get; set; } = new();

    /// <summary>"Let them read the screen": pets may talk and write about the text on the window they're on, in front of or beside.</summary>
    public bool ReadingEnabled { get; set; }

    /// <summary>"Let them leave notes": off stops new notes. Notes already up stay until dismissed.</summary>
    public bool NotesEnabled { get; set; }

    /// <summary>"Let them talk": off hides the bubbles and stops a spoken line in flight. Notes carry on (see <see cref="NotesEnabled"/>).</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                foreach (var bubble in _bubbles.Values)
                {
                    bubble.Silence();
                }
            }
        }
    }

    /// <summary>
    /// Shows a fixed line in the pet's bubble (no model involved), if talking is on: what pets say on a breeding pill
    /// round.
    /// </summary>
    public void Say(int petId, string text)
    {
        if (_enabled && _pets().FirstOrDefault(p => p.Id == petId) is { } pet)
        {
            Bubble(pet, text);
        }
    }

    /// <summary>Loads the model (in the background) and starts ticking.</summary>
    /// <param name="modelPath">The model file, or null if it wasn't found (chat stays quiet).</param>
    /// <param name="searched">Where it was looked for, for the notification when it's missing.</param>
    public void Start(string? modelPath, IReadOnlyList<string> searched)
    {
        CompositionTarget.Rendering += OnRendering;
        if (modelPath is null)
        {
            _notify("Chat is off: no model", "The chat model wasn't found. Looked in:\n" + string.Join("\n", searched), true);
            return;
        }

        _ = LoadAsync(modelPath, Rules.UseGpu, Rules.ContextTokens);
    }

    /// <summary>A pet was removed: it stops talking and its bubble goes.</summary>
    public void Forget(int petId)
    {
        _director.Forget(petId);
        if (_bubbles.Remove(petId, out var bubble))
        {
            bubble.Close();
        }
    }

    /// <summary>Keeps the bubbles above the pets after they were raised.</summary>
    public void BringToFront()
    {
        foreach (var bubble in _bubbles.Values)
        {
            bubble.BringToFront();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CompositionTarget.Rendering -= OnRendering;
        _director.Dispose();
        foreach (var window in _bubbles.Values.Cast<System.Windows.Window>().Concat(_notes))
        {
            window.Close();
        }

        _bubbles.Clear();
        _notes.Clear();
        _model?.Dispose();
    }

    private async Task LoadAsync(string path, bool useGpu, int contextTokens)
    {
        try
        {
            var model = await LlamaChatModel.LoadAsync(path, useGpu, contextTokens).ConfigureAwait(true); // back on the UI thread
            if (_disposed)
            {
                model.Dispose();
                return;
            }

            _model = model;
            _director.Model = model;
            Debug.WriteLine($"Deskling: chat model loaded ({model.Backend}, GPU: {model.OnGpu})");
            if (useGpu && !model.OnGpu)
            {
                _notify("Chat runs on the processor", "No usable graphics card was found, so the chat model runs on the processor. Replies may be slow.", false);
            }
        }
#pragma warning disable CA1031 // a model that won't load just leaves chat off; the pets carry on
        catch (Exception e)
#pragma warning restore CA1031
        {
            _notify("Chat is off: the model didn't load", $"{Path.GetFileName(path)}: {e.Message}", true);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var dt = System.Math.Min((now - _last).TotalSeconds, 0.25); // after a stall, don't jump ahead
        _last = now;

        var pets = _pets();
        var chatPets = pets.Select(p => p.ChatInfo).ToList();
        foreach (var pet in pets)
        {
            if (pet.TakeClick() is { } click)
            {
                _director.Clicked(pet.Id, click);
            }
        }

        var present = !pets.Any(p => p.UserAway); // nobody to read them while the screen is locked
        _director.Reader = present && ReadingEnabled ? _reader : null;
        _director.Tick(dt, chatPets, Rules, speech: present && _enabled, notes: present && NotesEnabled, _notes.Count);

        foreach (var line in _director.TakeLines())
        {
            Show(line, pets);
        }

        foreach (var problem in _director.TakeProblems())
        {
            _notify("A chat reply failed", problem, true);
        }

        var monitors = _desktop.GetSnapshot().Monitors;
        var placed = new List<RectI>();
        foreach (var id in _spoke.ToList())
        {
            var pet = pets.FirstOrDefault(p => p.Id == id);
            var feet = pet?.Feet ?? default;
            var area = (WorldQuery.MonitorAt(monitors, feet) ?? WorldQuery.Nearest(monitors, feet))?.WorkArea ?? default;
            if (pet is not null && _bubbles.TryGetValue(id, out var bubble) &&
                bubble.Follow(dt, new Vec2(feet.X, feet.Y - pet.SizePx), area, placed) is { } rect)
            {
                placed.Add(rect);
            }
            else
            {
                _spoke.Remove(id); // gone or finished
            }
        }

        if (_sinceNotes.Elapsed >= NoteInterval)
        {
            _sinceNotes.Restart();
            TrackNotes();
        }
    }

    /// <summary>Our own windows with text or art in them (pets, bubbles, notes), so a pet doesn't read them as the window's.</summary>
    private List<RectI> OwnWindows()
    {
        var rects = new List<RectI>();
        foreach (var pet in _pets())
        {
            var size = (int)System.Math.Ceiling(pet.SizePx);
            rects.Add(RectI.FromSize((int)(pet.Feet.X - (size / 2.0)), (int)pet.Feet.Y - size, size, size));
        }

        foreach (var window in _bubbles.Values.Cast<System.Windows.Window>().Concat(_notes))
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (handle != 0 && window.IsVisible && WindowTracker.Query(handle) is (TrackedWindowState.Shown, var bounds))
            {
                rects.Add(bounds);
            }
        }

        return rects;
    }

    private void Show(ChatLine line, IReadOnlyList<PetHost> pets)
    {
        var pet = pets.FirstOrDefault(p => p.Id == line.PetId);
        if (pet is null)
        {
            return;
        }

        if (line.Kind == ChatKind.Note && line.WindowId is { } windowId)
        {
            AddNote(pet, windowId, line);
            return;
        }

        Bubble(pet, line.Text);
    }

    private void Bubble(PetHost pet, string text)
    {
        if (!_bubbles.TryGetValue(pet.Id, out var bubble))
        {
            bubble = new SpeechBubble { Left = -32000, Top = -32000 };
            _bubbles[pet.Id] = bubble;
        }

        bubble.Say(text, Rules.BubbleDuration(text));
        _spoke.Remove(pet.Id);
        _spoke.Add(pet.Id);
    }

    /// <summary>
    /// A note on the window the pet stands on, just below its title bar, about where the pet is; or, on a window behind
    /// the pet, just above its head (below the title bar would often be far away, or under the window it stands on).
    /// </summary>
    private void AddNote(PetHost pet, long windowId, ChatLine line)
    {
        var (state, bounds) = WindowTracker.Query(windowId);
        if (state != TrackedWindowState.Shown || _notes.Count >= Rules.MaxNotes)
        {
            return;
        }

        var scale = pet.SizePx / System.Math.Max(1, pet.Config.SizeDip);
        var sameWindow = _notes.Count(n => n.WindowId == windowId);
        var x = line.NoteX - bounds.Left - (85 * scale); // centred on the pet
        var offset = line.NoteBehind
            ? new Vec2(x, line.NoteY - pet.SizePx - ((4 + (24 * sameWindow)) * scale) - bounds.Top) // its bottom, staggered upwards
            : new Vec2(x, (44 + (24 * sameWindow)) * scale); // its top, staggered downwards
        var note = new StickyNote(windowId, offset, line.Text, pet.Name, fromBottom: line.NoteBehind) { Left = -32000, Top = -32000 };
        note.Dismissed += (_, _) => RemoveNote(note);
        _notes.Add(note);
        TrackNotes(); // on top of the window's other notes
    }

    private void RemoveNote(StickyNote note)
    {
        _notes.Remove(note);
        note.Close();
    }

    /// <summary>
    /// Follows each note's window. A window's notes stack oldest first, each just above the one before, so overlapping
    /// notes keep a fixed order instead of taking turns being directly above the window.
    /// </summary>
    private void TrackNotes()
    {
        foreach (var group in _notes.GroupBy(n => n.WindowId).ToList())
        {
            var (state, bounds) = WindowTracker.Query(group.Key);
            if (state == TrackedWindowState.Gone)
            {
                foreach (var note in group.ToList())
                {
                    RemoveNote(note);
                }

                continue;
            }

            var below = group.Key;
            foreach (var note in group)
            {
                if (note.Track(state, bounds, below))
                {
                    below = note.Handle;
                }
            }
        }
    }
}

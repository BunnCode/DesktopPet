using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Traits;

namespace DesktopPet.Core.Chat;

/// <summary>A pet as chat sees it, gathered by the host each tick.</summary>
/// <param name="Id">The pet's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Personality">Its personality text.</param>
/// <param name="Stats">Its needs.</param>
/// <param name="Feet">Where it is (physical pixels).</param>
/// <param name="Scale">The DPI scale of its monitor.</param>
/// <param name="CanTalk">False while it's asleep (it neither talks nor writes notes).</param>
/// <param name="WindowId">The window it stands on, if any (sticky notes go there).</param>
/// <param name="Traits">How often it talks and writes notes.</param>
/// <param name="ReadWindowId">The window it's on, in front of or beside, which it can read (M12b), if any.</param>
/// <param name="BehindWindowId">
/// The window it stands in front of (not on), if any: sticky notes go there when it isn't standing on one.
/// </param>
public sealed record ChatPet(
    int Id, string Name, string Personality, PetStats Stats, Vec2 Feet, double Scale, bool CanTalk, long? WindowId, ChatTraits Traits,
    long? ReadWindowId = null, long? BehindWindowId = null);

/// <summary>
/// What came of a request: a spoken line for a speech bubble, or (<see cref="ChatKind.Note"/>) the text of a sticky
/// note for <see cref="WindowId"/> at <see cref="NoteX"/>. A note on a window the pet stood on goes below its title bar;
/// one on a window behind the pet (<see cref="NoteBehind"/>) goes just above the pet's head, its feet then being at
/// <see cref="NoteY"/>.
/// </summary>
public sealed record ChatLine(
    int PetId, ChatKind Kind, string Text, long? WindowId = null, double NoteX = 0, double NoteY = 0, bool NoteBehind = false);

/// <summary>
/// Runs the two behaviors that use the language model, <b>speech</b> (bubbles: needs, banter and its answer, jokes)
/// and <b>sticky notes</b>. They're independent: each has its own switch, rates, gaps and memory of what was said or
/// written, so one never holds the other back. What they share is the model, asked <b>one request at a time</b>
/// (one model serves every pet). The pets keep acting while it waits; a slow or failed reply is skipped. Ticked by the
/// host with real elapsed time; results are collected with <see cref="TakeLines"/>.
/// </summary>
public sealed class ChatDirector : IDisposable
{
    private readonly IRandom _random;
    private readonly List<ChatLine> _lines = [];
    private readonly List<string> _problems = [];
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly Track _speech = new();
    private readonly Track _notes = new();
    private readonly Dictionary<int, (PetClick Click, double At)> _clicks = [];
    private double _now;
    private Job? _job;
    private PendingAnswer? _answer;
    private (ChatLine Line, double ShowAt)? _held;

    public ChatDirector(IRandom random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>The model; null until it has loaded (nothing happens until then).</summary>
    public IChatModel? Model { get; set; }

    /// <summary>
    /// Reads the screen for remarks and notes about what a pet sees (M12b); null while reading is off. A read that
    /// already started finishes.
    /// </summary>
    public IScreenReader? Reader { get; set; }

    /// <summary>A request is in flight.</summary>
    public bool Busy => _job is not null;

    /// <summary>
    /// Advances time, collects a finished reply, and maybe starts the next request: a reaction to a click first, then a
    /// pending answer, then a spoken line, then a note.
    /// </summary>
    /// <param name="dt">Seconds since the last tick.</param>
    /// <param name="pets">Every pet now.</param>
    /// <param name="rules">Chat tuning.</param>
    /// <param name="speech">Off: no speech, and a spoken line in flight or waiting is dropped.</param>
    /// <param name="notes">Off: no new notes, and a note in flight is dropped.</param>
    /// <param name="notesOnScreen">How many sticky notes are showing.</param>
    public void Tick(double dt, IReadOnlyList<ChatPet> pets, ChatRules rules, bool speech, bool notes, int notesOnScreen)
    {
        ArgumentNullException.ThrowIfNull(pets);
        ArgumentNullException.ThrowIfNull(rules);
        _now += System.Math.Max(0, dt);

        if (!speech)
        {
            CancelSpeech();
        }

        if (!notes && _job is { Kind: ChatKind.Note } noteJob)
        {
            noteJob.Abandon();
        }

        if (_held is { } held && _now >= held.ShowAt)
        {
            _held = null;
            if (pets.Any(p => p.Id == held.Line.PetId))
            {
                Deliver(held.Line, rules);
            }
        }

        if (_job is { } job)
        {
            if (job.Task.IsCompleted)
            {
                _job = null;
                Finish(job, pets, rules);
            }
            else if (!job.Abandoned && _now - job.StartedAt > rules.ReplyTimeoutSeconds)
            {
                job.Abandon(); // the model stops soon; nothing new starts until it has
            }

            if (_job is not null)
            {
                return;
            }
        }

        if (Model is null)
        {
            return;
        }

        if (speech && TryReactToClick(pets, rules))
        {
            return;
        }

        if (speech && _held is null && _answer is { } answer)
        {
            _answer = null;
            if (pets.FirstOrDefault(p => p.Id == answer.ListenerId) is { CanTalk: true } listener)
            {
                Start(listener, ChatKind.Answer, ChatPrompts.Answer(answer.SpeakerName, answer.Line), rules, answerAt: answer.ShowAt);
                return;
            }
        }

        // An answer waiting to be shown holds back new speech (the conversation isn't over), not notes.
        if (speech && _held is null && _answer is null && _now - _speech.LastAt >= rules.MinGapSeconds)
        {
            foreach (var pet in pets)
            {
                if (TrySpeak(pet, pets, rules, dt))
                {
                    return;
                }
            }
        }

        if (notes && notesOnScreen < rules.MaxNotes && _now - _notes.LastAt >= rules.NoteGapSeconds)
        {
            foreach (var pet in pets)
            {
                if (TryWriteNote(pet, rules, dt))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// The user clicked the pet (not a drag): it says something about it as soon as the model is free, skipping the
    /// usual gaps. A newer click on the same pet replaces one still waiting.
    /// </summary>
    public void Clicked(int petId, PetClick click) => _clicks[petId] = (click, _now);

    /// <summary>Results finished since the last call.</summary>
    public IReadOnlyList<ChatLine> TakeLines()
    {
        var lines = _lines.ToArray();
        _lines.Clear();
        return lines;
    }

    /// <summary>Model failures since the last call, each distinct message once per session.</summary>
    public IReadOnlyList<string> TakeProblems()
    {
        var problems = _problems.ToArray();
        _problems.Clear();
        return problems;
    }

    /// <summary>A pet was removed: drop anything it was about to say, hear or write.</summary>
    public void Forget(int petId)
    {
        if (_job is { } job && (job.PetId == petId || job.ListenerId == petId))
        {
            job.Abandon();
        }

        if (_answer is { } answer && answer.ListenerId == petId)
        {
            _answer = null;
        }

        if (_held is { } held && held.Line.PetId == petId)
        {
            _held = null;
        }

        _speech.Forget(petId);
        _notes.Forget(petId);
        _clicks.Remove(petId);
    }

    /// <summary>What this pet said lately (oldest first).</summary>
    public IReadOnlyCollection<string> Recent(int petId) => _speech.Recent(petId);

    /// <summary>The notes this pet wrote lately (oldest first).</summary>
    public IReadOnlyCollection<string> RecentNotes(int petId) => _notes.Recent(petId);

    /// <summary>Cancels a request in flight. Its token source is left to the task, which may still be reading it.</summary>
    public void Dispose()
    {
        _job?.Abandon();
        _answer = null;
        _held = null;
    }

    private void CancelSpeech()
    {
        if (_job is { Kind: not ChatKind.Note } job)
        {
            job.Abandon();
        }

        _answer = null;
        _held = null;
        _clicks.Clear();
    }

    /// <summary>Starts the reaction to the oldest click still waiting; clicks waiting too long are dropped.</summary>
    private bool TryReactToClick(IReadOnlyList<ChatPet> pets, ChatRules rules)
    {
        foreach (var (id, (click, at)) in _clicks.OrderBy(c => c.Value.At).ToList())
        {
            _clicks.Remove(id);
            if (_now - at <= rules.ClickLineWithinSeconds && pets.FirstOrDefault(p => p.Id == id) is { } pet)
            {
                Start(pet, ChatKind.Clicked, ChatPrompts.Clicked(click), rules); // even if it was asleep: the click woke it
                return true;
            }
        }

        return false;
    }

    /// <summary>Rolls whether this pet says something now, and what about.</summary>
    private bool TrySpeak(ChatPet pet, IReadOnlyList<ChatPet> pets, ChatRules rules, double dt)
    {
        if (!pet.CanTalk || _now - _speech.LastFor(pet.Id) < rules.PetGapSeconds)
        {
            return false;
        }

        var t = pet.Traits;
        var needy = LowestNeed(pet.Stats, rules.NeedyBelow);
        var friend = Nearest(pet, pets, rules.BanterDistanceDip * pet.Scale);
        var f = rules.ChatFrequency;
        var rates = new (ChatKind Kind, double PerHour)[]
        {
            (ChatKind.Needy, needy is null ? 0 : rules.NeedyPerHour * t.Needy * f),
            (ChatKind.Banter, friend is null ? 0 : rules.BanterPerHour * t.Banter * f),
            (ChatKind.Joke, rules.JokesPerHour * t.Jokes * f),
            (ChatKind.Remark, Reader is null || pet.ReadWindowId is null ? 0 : rules.RemarksPerHour * t.Curiosity * f),
        };

        var total = rates.Sum(r => System.Math.Max(0, r.PerHour));
        if (total <= 0 || !_random.Chance(total * dt / 3600))
        {
            return false;
        }

        var pick = _random.NextDouble() * total;
        var kind = ChatKind.Joke;
        foreach (var (k, perHour) in rates)
        {
            if (perHour <= 0)
            {
                continue;
            }

            kind = k;
            pick -= perHour;
            if (pick < 0)
            {
                break;
            }
        }

        switch (kind)
        {
            case ChatKind.Needy:
                var (need, value) = needy!.Value;
                Start(pet, kind, ChatPrompts.Needy(need, value), rules);
                break;
            case ChatKind.Banter:
                Start(pet, kind, ChatPrompts.Banter(friend!.Name), rules, listener: friend);
                break;
            case ChatKind.Remark:
                Start(pet, kind, user: null, rules, read: new Reading(pet.ReadWindowId!.Value, ChatPrompts.Remark));
                break;
            default:
                Start(pet, ChatKind.Joke, ChatPrompts.Joke, rules);
                break;
        }

        return true;
    }

    /// <summary>Rolls whether this pet, standing on or in front of a window, leaves a note on it now.</summary>
    private bool TryWriteNote(ChatPet pet, ChatRules rules, double dt)
    {
        if (!pet.CanTalk || (pet.WindowId ?? pet.BehindWindowId) is not { } window || _now - _notes.LastFor(pet.Id) < rules.PetNoteGapSeconds)
        {
            return false;
        }

        var perHour = rules.NotesPerHour * pet.Traits.Notes;
        if (perHour <= 0 || !_random.Chance(perHour * dt / 3600))
        {
            return false;
        }

        var topics = ChatPrompts.NoteTopics;
        var topic = topics[System.Math.Min(topics.Count - 1, (int)(_random.NextDouble() * topics.Count))];
        var usual = ChatPrompts.Note(topic);
        var read = Reader is not null && _random.Chance(System.Math.Clamp(rules.NoteReadChance * pet.Traits.Curiosity, 0, 1))
            ? new Reading(window, ChatPrompts.NoteAbout)
            : null;
        Start(pet, ChatKind.Note, usual, rules, noteWindow: window, noteBehind: pet.WindowId is null, read: read); // nothing readable: the usual topic
        return true;
    }

    /// <param name="pet">Who speaks or writes.</param>
    /// <param name="kind">What kind of line.</param>
    /// <param name="user">What to say; with <paramref name="read"/>, what to say when nothing could be read (null: say nothing).</param>
    /// <param name="rules">Chat tuning.</param>
    /// <param name="listener">Who a banter line is said to.</param>
    /// <param name="noteWindow">The window a note goes on.</param>
    /// <param name="noteBehind">The note's window is behind the pet rather than under its feet.</param>
    /// <param name="answerAt">When an answer may be shown.</param>
    /// <param name="read">Read this window first and ask about what's there.</param>
    private void Start(
        ChatPet pet, ChatKind kind, string? user, ChatRules rules, ChatPet? listener = null, long? noteWindow = null,
        bool noteBehind = false, double answerAt = 0, Reading? read = null)
    {
        var system = kind == ChatKind.Note
            ? ChatPrompts.NoteSystem(pet.Name, pet.Personality, _notes.Recent(pet.Id))
            : ChatPrompts.System(pet.Name, pet.Personality, kind is ChatKind.Needy or ChatKind.Clicked ? pet.Stats : null, _speech.Recent(pet.Id));
        var cancel = new CancellationTokenSource();
        Task<string> task;
        try
        {
            task = read is not null && Reader is { } reader
                ? ReadThenAskAsync(reader, Model!, read, pet, system, user, rules, cancel.Token)
                : Model!.CompleteAsync(new ChatPrompt(system, user ?? string.Empty), rules.Sampling, cancel.Token);
        }
#pragma warning disable CA1031 // any model failure just skips this one
        catch (Exception e)
#pragma warning restore CA1031
        {
            task = Task.FromException<string>(e);
        }

        _job = new Job(pet.Id, pet.Name, kind, task, cancel, _now, listener?.Id, noteWindow, pet.Feet, noteBehind, answerAt);
        TrackFor(kind).Mark(pet.Id, _now);
    }

    /// <summary>
    /// Reads the window, then asks the model about the nearest text there. With nothing readable (an excluded window,
    /// no text, no OCR) it asks <paramref name="fallback"/> instead, or says nothing (an empty reply) without one.
    /// The text read only goes into this one prompt.
    /// </summary>
    private static async Task<string> ReadThenAskAsync(
        IScreenReader reader, IChatModel model, Reading read, ChatPet pet, string system, string? fallback, ChatRules rules,
        CancellationToken cancellationToken)
    {
        ScreenText? seen;
        try
        {
            seen = await reader.ReadAsync(read.WindowId, pet.Feet, pet.Scale, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // a failed read is just nothing read
        catch (Exception)
#pragma warning restore CA1031
        {
            seen = null;
        }

        var snippet = seen is null ? null : ScreenReading.Snippet(seen.Lines, pet.Feet, rules.ReadChars, seen.Title);
        var user = snippet is not null ? read.Prompt(seen!.Title, snippet) : fallback;
        if (user is null)
        {
            return string.Empty;
        }

        return await model.CompleteAsync(new ChatPrompt(system, user), rules.Sampling, cancellationToken).ConfigureAwait(false);
    }

    private void Finish(Job job, IReadOnlyList<ChatPet> pets, ChatRules rules)
    {
        job.Dispose();
        if (job.Task.IsFaulted)
        {
            var message = job.Task.Exception?.GetBaseException().Message ?? "unknown error";
            if (_reported.Add(message))
            {
                _problems.Add(message);
            }

            return;
        }

        if (job.Abandoned || !job.Task.IsCompletedSuccessfully || pets.All(p => p.Id != job.PetId))
        {
            return;
        }

        var text = ChatText.Clean(job.Task.Result, job.PetName, rules.MaxSentences, rules.MaxChars);
        if (text is null)
        {
            return;
        }

        var line = new ChatLine(job.PetId, job.Kind, text, job.NoteWindow, job.NoteAt.X, job.NoteAt.Y, job.NoteBehind);
        if (job.Kind == ChatKind.Answer && _now < job.AnswerAt)
        {
            _held = (line, job.AnswerAt); // let the first line be read before the answer appears
            return;
        }

        Deliver(line, rules);
        if (job.Kind == ChatKind.Banter && job.ListenerId is { } listener && pets.Any(p => p.Id == listener))
        {
            _answer = new PendingAnswer(listener, job.PetName, text, _now + rules.AnswerDelaySeconds);
        }
    }

    private void Deliver(ChatLine line, ChatRules rules)
    {
        _lines.Add(line);
        var track = TrackFor(line.Kind);
        track.Mark(line.PetId, _now);
        track.Remember(line.PetId, line.Text, rules.RecentLines);
    }

    private Track TrackFor(ChatKind kind) => kind == ChatKind.Note ? _notes : _speech;

    /// <summary>The lowest need under the threshold, if any.</summary>
    internal static (NeedKind Need, double Value)? LowestNeed(PetStats stats, double below)
    {
        var needs = new[] { (NeedKind.Hunger, stats.Hunger), (NeedKind.Happiness, stats.Happiness), (NeedKind.Energy, stats.Energy) };
        var lowest = needs.MinBy(n => n.Item2);
        return lowest.Item2 < below ? lowest : null;
    }

    /// <summary>The nearest other pet within reach that can talk.</summary>
    private static ChatPet? Nearest(ChatPet pet, IReadOnlyList<ChatPet> pets, double reach) =>
        pets.Where(p => p.Id != pet.Id && p.CanTalk && (p.Feet - pet.Feet).Length <= reach)
            .MinBy(p => (p.Feet - pet.Feet).Length);

    /// <summary>One behavior's timing and memory: when anyone last did it, when each pet did, and what each pet produced lately.</summary>
    private sealed class Track
    {
        private readonly Dictionary<int, double> _byPet = [];
        private readonly Dictionary<int, Queue<string>> _recent = [];

        public double LastAt { get; private set; } = double.NegativeInfinity;

        public double LastFor(int petId) => _byPet.TryGetValue(petId, out var at) ? at : double.NegativeInfinity;

        public void Mark(int petId, double now)
        {
            LastAt = now;
            _byPet[petId] = now;
        }

        public void Remember(int petId, string text, int keep)
        {
            if (!_recent.TryGetValue(petId, out var recent))
            {
                recent = new Queue<string>();
                _recent[petId] = recent;
            }

            recent.Enqueue(text);
            while (recent.Count > keep)
            {
                recent.Dequeue();
            }
        }

        public IReadOnlyCollection<string> Recent(int petId) =>
            _recent.TryGetValue(petId, out var lines) ? lines : Array.Empty<string>();

        public void Forget(int petId)
        {
            _byPet.Remove(petId);
            _recent.Remove(petId);
        }
    }

    /// <summary>A window to read before asking, and the prompt to ask with (title, text read).</summary>
    private sealed record Reading(long WindowId, Func<string, string, string> Prompt);

    private sealed record PendingAnswer(int ListenerId, string SpeakerName, string Line, double ShowAt);

    private sealed class Job(
        int petId, string petName, ChatKind kind, Task<string> task, CancellationTokenSource cancel, double startedAt, int? listenerId,
        long? noteWindow, Vec2 noteAt, bool noteBehind, double answerAt) : IDisposable
    {
        public int PetId { get; } = petId;
        public string PetName { get; } = petName;
        public ChatKind Kind { get; } = kind;
        public Task<string> Task { get; } = task;
        public double StartedAt { get; } = startedAt;
        public int? ListenerId { get; } = listenerId;
        public long? NoteWindow { get; } = noteWindow;
        public Vec2 NoteAt { get; } = noteAt;
        public bool NoteBehind { get; } = noteBehind;
        public double AnswerAt { get; } = answerAt;
        public bool Abandoned { get; private set; }

        public void Abandon()
        {
            if (!Abandoned)
            {
                Abandoned = true;
                cancel.Cancel();
            }
        }

        public void Dispose() => cancel.Dispose();
    }
}

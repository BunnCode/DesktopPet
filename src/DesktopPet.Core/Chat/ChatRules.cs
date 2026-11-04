namespace DesktopPet.Core.Chat;

/// <summary>
/// Tuning for the two behaviors that use the language model (<c>settings.json</c> → <c>Chat</c>): <b>speech</b>
/// (bubbles) and <b>sticky notes</b>. They're separate: each has its own switch, rates and gaps. The model settings are
/// shared. Rates are "per hour while it makes sense", scaled by each pet's <see cref="Traits.ChatTraits"/>.
/// </summary>
public sealed record ChatRules
{
    // ---- The model (shared) ----

    /// <summary>
    /// The model file (GGUF). A bare file name is looked for in the <c>models</c> folder next to the app, then in the
    /// data folder's <c>models</c> folder; a full path is used as is. Applies on restart.
    /// </summary>
    public string ModelFile { get; init; } = "Qwen3.5-2B-Q6_K.gguf";

    /// <summary>Run the model on the graphics card (falls back to the processor when there's none). Applies on restart.</summary>
    public bool UseGpu { get; init; } = true;

    /// <summary>Longest reply, in tokens (a token is about three quarters of a word).</summary>
    public int MaxTokens { get; init; } = 60;

    /// <summary>Randomness of the wording: 0 is always the likeliest words, 1 and above is wilder.</summary>
    public double Temperature { get; init; } = 0.8;

    /// <summary>A reply that takes longer than this is dropped.</summary>
    public double ReplyTimeoutSeconds { get; init; } = 25;

    // ---- Speech bubbles ----

    /// <summary>Whether pets talk. The menu's "Let them talk" starts from this.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>A need below this makes the pet say so.</summary>
    public double NeedyBelow { get; init; } = 30;

    /// <summary>Lines per hour about a low need, while one is low.</summary>
    public double NeedyPerHour { get; init; } = 10;

    /// <summary>Conversations per hour while another pet is close by (one speaks, the other replies).</summary>
    public double BanterPerHour { get; init; } = 8;

    /// <summary>"Close by" for banter, DIP between the two pets' feet.</summary>
    public double BanterDistanceDip { get; init; } = 260;

    /// <summary>Jokes per hour.</summary>
    public double JokesPerHour { get; init; } = 3;

    /// <summary>A pet reacts out loud to every click (not drags), skipping the gaps below, unless the model can't get to it within this long.</summary>
    public double ClickLineWithinSeconds { get; init; } = 8;

    /// <summary>At least this long between two spoken lines from any pets (answers excepted).</summary>
    public double MinGapSeconds { get; init; } = 20;

    /// <summary>At least this long between two spoken lines from the same pet (answers excepted).</summary>
    public double PetGapSeconds { get; init; } = 60;

    /// <summary>The answer in a conversation appears at least this long after the line it answers.</summary>
    public double AnswerDelaySeconds { get; init; } = 2;

    /// <summary>A speech bubble stays this long, plus <see cref="BubbleSecondsPerChar"/> for each character.</summary>
    public double BubbleSeconds { get; init; } = 3;

    public double BubbleSecondsPerChar { get; init; } = 0.06;

    /// <summary>How many of a pet's own recent lines (and, separately, notes) it's reminded of, so it doesn't repeat itself.</summary>
    public int RecentLines { get; init; } = 5;

    // ---- Sticky notes ----

    /// <summary>Whether pets leave sticky notes. The menu's "Let them leave notes" starts from this.</summary>
    public bool NotesEnabled { get; init; } = true;

    /// <summary>Notes per hour while the pet stands on a window.</summary>
    public double NotesPerHour { get; init; } = 2;

    /// <summary>At most this many sticky notes on screen at once.</summary>
    public int MaxNotes { get; init; } = 3;

    /// <summary>At least this long between two notes from any pets.</summary>
    public double NoteGapSeconds { get; init; } = 120;

    /// <summary>At least this long between two notes from the same pet.</summary>
    public double PetNoteGapSeconds { get; init; } = 600;

    /// <summary>How long a speech bubble with <paramref name="text"/> stays, in seconds.</summary>
    public double BubbleDuration(string text) =>
        BubbleSeconds + (BubbleSecondsPerChar * (text?.Length ?? 0));
}

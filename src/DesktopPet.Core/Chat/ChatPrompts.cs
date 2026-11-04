using System.Text;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Chat;

/// <summary>The kinds of line a pet says.</summary>
public enum ChatKind
{
    /// <summary>Says a need is low.</summary>
    Needy,

    /// <summary>Starts a conversation with a pet close by.</summary>
    Banter,

    /// <summary>Answers a <see cref="Banter"/> line.</summary>
    Answer,

    /// <summary>Reacts to being clicked.</summary>
    Clicked,

    Joke,

    /// <summary>Remarks on something it reads on the window it's on, in front of or beside (M12b).</summary>
    Remark,

    /// <summary>Not speech: the text of a sticky note left on a window (its own behavior, see <see cref="ChatDirector"/>).</summary>
    Note,
}

/// <summary>Which need a <see cref="ChatKind.Needy"/> line is about.</summary>
public enum NeedKind
{
    Hunger,
    Happiness,
    Energy,
}

/// <summary>
/// Builds the prompts. Written for a small model: one plain instruction per prompt, the pet's personality and mood in
/// the system message, and its recent lines so it doesn't repeat itself.
/// </summary>
public static class ChatPrompts
{
    /// <summary>
    /// Who the pet is and how it talks; shared by every kind of line. The mood is only given for lines about needs:
    /// a small model otherwise turns every joke and greeting into a complaint.
    /// </summary>
    public static string System(string name, string personality, PetStats? mood, IReadOnlyCollection<string> recent)
    {
        ArgumentNullException.ThrowIfNull(recent);
        var text = new StringBuilder();
        text.Append("You are ").Append(name).Append(", a tiny rabbit who lives on the user's computer desktop. ");
        if (!string.IsNullOrWhiteSpace(personality))
        {
            text.Append(personality.Trim()).Append(' ');
        }

        if (mood is { } stats)
        {
            text.Append('\n').Append("Right now you feel ").Append(Mood(stats)).Append('.');
        }

        if (recent.Count > 0)
        {
            text.Append('\n').Append("Things you said lately (say something different): ");
            text.AppendJoin(" / ", recent);
        }

        text.Append('\n')
            .Append("Reply with only the words ").Append(name).Append(" says out loud: one or two short, casual sentences, ")
            .Append("under 75 words.");
        return text.ToString();
    }

    public static string Needy(NeedKind need, double value)
    {
        var very = value < 15 ? "very " : string.Empty;
        return need switch
        {
            NeedKind.Hunger => $"You are {very}hungry. Tell the user you want something to eat.",
            NeedKind.Happiness => $"You are {very}bored and lonely. Ask the user to play with you or give you some attention.",
            _ => $"You are {very}tired. Tell the user you're sleepy.",
        };
    }

    /// <summary>A reaction to being clicked, depending on what the click did.</summary>
    public static string Clicked(Simulation.PetClick click) => click switch
    {
        Simulation.PetClick.WokenUp => "The user just woke you up from a nap by clicking on you. React to being woken up.",
        Simulation.PetClick.Poked => "The user just poked you by clicking on you while you were busy climbing or falling. React to the poke.",
        _ => "The user just petted you by clicking on you. React to being petted.",
    };

    public static string Banter(string other) =>
        $"Your friend {other}, another little rabbit, is right next to you. Say something to {other}.";

    public static string Answer(string other, string line) =>
        $"Your friend {other} just said to you: \"{line}\" Answer {other}.";

    public const string Joke = "Tell the user a short, silly joke about whatever the other pets have been talking about recently.";

    /// <summary>
    /// For a sticky note, which is written, not spoken: who the pet is, and that the answer is the note's text alone.
    /// No mood: notes are for the user, not about the pet's needs.
    /// </summary>
    public static string NoteSystem(string name, string personality, IReadOnlyCollection<string> recentNotes)
    {
        ArgumentNullException.ThrowIfNull(recentNotes);
        var text = new StringBuilder();
        text.Append("You are ").Append(name).Append(", a tiny rabbit who lives on the user's computer desktop. ");
        if (!string.IsNullOrWhiteSpace(personality))
        {
            text.Append(personality.Trim()).Append(' ');
        }

        text.Append('\n').Append("You write little sticky notes and stick them on the user's windows.");
        if (recentNotes.Count > 0)
        {
            text.Append('\n').Append("Notes you wrote lately (write something different): ");
            text.AppendJoin(" / ", recentNotes);
        }

        text.Append('\n')
            .Append("Reply with only the text written on the note: one short sentence, under 15 words. ")
            .Append("Don't talk about the note or the window. No emojis, no hashtags, no quotation marks.");
        return text.ToString();
    }

    /// <summary>
    /// What a note is about, one picked per note. A small model given a vague "a reminder, a kind word or a silly
    /// thought" tends to write that phrase back; one concrete subject works.
    /// </summary>
    public static IReadOnlyList<string> NoteTopics { get; } =
    [
        "reminds the user to drink some water",
        "reminds the user to stretch",
        "tells the user to take a short break",
        "cheers the user on with their work",
        "says something kind to the user",
        "reminds the user to look away from the screen for a moment",
        "is a silly thought about carrots",
        "says the user is doing great today",
        "is a cozy thought about naps",
        "asks the user to come and play later",
    ];

    /// <summary>The note to write, about one of <see cref="NoteTopics"/>.</summary>
    public static string Note(string topic) => $"Write a sticky note for the user that {topic}.";

    /// <summary>A spoken remark about text the pet reads on the window it's on, in front of or beside.</summary>
    public static string Remark(string title, string text) =>
        Reading(title, text) +
        "Say something to the user about one thing you read there, in your own words. Don't read it out.";

    /// <summary>A sticky note about text the pet reads on the window it's on.</summary>
    public static string NoteAbout(string title, string text) =>
        Reading(title, text) +
        "Write a sticky note for the user about one thing you read there: a friendly comment, tip or question, in your own words.";

    /// <summary>Where the pet is and what it reads, the text set apart so the model sees where it starts and ends.</summary>
    private static string Reading(string title, string text)
    {
        var where = string.IsNullOrWhiteSpace(title) ? "on one of the user's windows" : $"on the user's window \"{title.Trim()}\"";
        return $"You're {where}. Near you, you can read this text on it:\n<<<\n{text.Trim()}\n>>>\n";
    }

    /// <summary>How the pet feels, in words: its low needs, or "fine" / "great".</summary>
    internal static string Mood(PetStats stats)
    {
        var parts = new List<string>();
        void Low(double value, string word)
        {
            if (value < 15)
            {
                parts.Add("very " + word);
            }
            else if (value < 50)
            {
                parts.Add(word);
            }
        }

        Low(stats.Hunger, "hungry");
        Low(stats.Happiness, "lonely");
        Low(stats.Energy, "sleepy");
        if (parts.Count > 0)
        {
            return string.Join(" and ", parts);
        }

        return stats.Lowest > 75 ? "great" : "fine";
    }
}

using Deskling.Core.Abstractions;

namespace Deskling.Core.Breeding;

/// <summary>Something a pet says on a breeding pill round, shown as a speech bubble (fixed lines, not the chat model).</summary>
public sealed record Saying(int PetId, string Text);

/// <summary>Why an asked pet says no, which picks its line.</summary>
internal enum NoReason
{
    /// <summary>It could, it just doesn't want to (the chance said no).</summary>
    NotInterested,
    Fixed,

    /// <summary>It mated not long ago.</summary>
    Cooldown,

    /// <summary>A need is too low.</summary>
    NotWell,
}

/// <summary>The lines of a breeding pill round (M15).</summary>
internal static class PillLines
{
    private static readonly string[] Asks =
    [
        "{0}, want to make babies?",
        "Hey {0}... babies?",
        "Be my mate, {0}?",
        "{0}! Let's start a family!",
    ];

    private static readonly string[] Yeses = ["Yes!", "Oh, yes please!", "I thought you'd never ask!", "Okay!"];

    private static readonly string[] NotInterested = ["No thanks.", "Not now.", "Ew, no!", "Maybe another time."];

    public static string Ask(IRandom random, string name) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, Pick(random, Asks), name);

    public static string Yes(IRandom random) => Pick(random, Yeses);

    public static string No(IRandom random, NoReason reason) => reason switch
    {
        NoReason.Fixed => "Sorry, I'm fixed.",
        NoReason.Cooldown => "I just had babies!",
        NoReason.NotWell => "I'm not feeling up to it.",
        _ => Pick(random, NotInterested),
    };

    private static string Pick(IRandom random, string[] lines) =>
        lines[System.Math.Clamp((int)(random.NextDouble() * lines.Length), 0, lines.Length - 1)];
}

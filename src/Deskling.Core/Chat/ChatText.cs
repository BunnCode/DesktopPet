using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Deskling.Core.Chat;

/// <summary>
/// Turns a raw model reply into a line fit for a speech bubble, or rejects it. Small models add quotes, name prefixes,
/// stage directions, emojis and run-on text; this strips them and keeps at most two sentences (by default).
/// </summary>
public static partial class ChatText
{
    /// <summary>Longest line shown by default, in characters (<see cref="ChatRules.MaxChars"/>).</summary>
    public const int MaxLength = 160;

    /// <summary>The cleaned line, or null when nothing usable is left.</summary>
    /// <param name="raw">What the model returned.</param>
    /// <param name="name">The speaking pet's name (a leading "Name:" is removed).</param>
    /// <param name="maxSentences">Keep at most this many sentences.</param>
    /// <param name="maxChars">Keep at most this many characters.</param>
    public static string? Clean(string? raw, string name, int maxSentences = 2, int maxChars = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = ThinkBlock().Replace(raw, " ");
        text = Tag().Replace(text, " ");
        text = Action().Replace(text, " ");
        text = RemoveSymbols(text);
        text = Whitespace().Replace(text, " ").Trim();

        // "Mia: Hello" / "Mia says: Hello" / "Rabbit: Hello"
        foreach (var prefix in new[] { name, "Rabbit", "Bunny", "Assistant" })
        {
            if (!string.IsNullOrEmpty(prefix) && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rest = text[prefix.Length..].TrimStart();
                if (rest.StartsWith("says:", StringComparison.OrdinalIgnoreCase))
                {
                    text = rest[5..].TrimStart();
                }
                else if (rest.StartsWith(':'))
                {
                    text = rest[1..].TrimStart();
                }
            }
        }

        // Narration around the line: Mia hops over and says, "Hello!" → Hello!
        if (Narrated().Match(text) is { Success: true } narrated)
        {
            text = narrated.Groups["line"].Value;
        }

        text = StripQuotes(text);
        text = FirstSentences(text, maxSentences);
        text = Truncate(text, maxChars);
        text = StripQuotes(text);
        return text.Count(char.IsLetter) >= 2 ? text : null;
    }

    private static string StripQuotes(string text)
    {
        const string quotes = "\"'“”‘’«»`";
        text = text.Trim();
        while (text.Length >= 2 && quotes.Contains(text[0], StringComparison.Ordinal) && quotes.Contains(text[^1], StringComparison.Ordinal))
        {
            text = text[1..^1].Trim();
        }

        // An unpaired quote at either end ("Hello there).
        if (text.Length > 0 && "\"“”".Contains(text[0], StringComparison.Ordinal) && !text[1..].Contains('"', StringComparison.Ordinal))
        {
            text = text[1..].TrimStart();
        }

        if (text.Length > 0 && "\"“”".Contains(text[^1], StringComparison.Ordinal) && !text[..^1].Contains('"', StringComparison.Ordinal))
        {
            text = text[..^1].TrimEnd();
        }

        return text;
    }

    /// <summary>Emojis and other pictographs (the bubble's font can't draw them in colour), keeping letters and punctuation.</summary>
    private static string RemoveSymbols(string text)
    {
        var kept = new StringBuilder(text.Length);
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            var element = e.GetTextElement();
            var category = CharUnicodeInfo.GetUnicodeCategory(element, 0);
            var isEmoji = char.IsSurrogate(element[0]) || category is UnicodeCategory.OtherSymbol or UnicodeCategory.Surrogate
                || element.Contains('️', StringComparison.Ordinal);
            if (!isEmoji && !(category is UnicodeCategory.Control && element[0] is not ('\n' or '\t')))
            {
                kept.Append(element);
            }
        }

        return kept.ToString();
    }

    private static string FirstSentences(string text, int count)
    {
        var parts = SentenceEnd().Split(text);
        return parts.Length <= count ? text : string.Join(" ", parts.Take(count));
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', max - 1);
        return text[..(cut > max / 2 ? cut : max - 1)].TrimEnd(',', ';', ':', ' ') + "…";
    }

    [GeneratedRegex(@"<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();

    [GeneratedRegex(@"<\|?/?[a-z_]+\|?>", RegexOptions.IgnoreCase)]
    private static partial Regex Tag();

    [GeneratedRegex(@"\*[^*]{0,80}\*")]
    private static partial Regex Action();

    [GeneratedRegex(@"^[^""“]{0,80}\b(says|said|asks|whispers|shouts|writes)\b[^""“]{0,20}[""“](?<line>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Narrated();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?<=[.!?…])\s+")]
    private static partial Regex SentenceEnd();
}

/// <summary>
/// The Qwen chat format (ChatML), with thinking switched off the way Qwen's own template does it: an empty think
/// block already in the answer.
/// </summary>
public static class ChatMl
{
    /// <summary>Ends an answer; generation stops here.</summary>
    public const string EndOfTurn = "<|im_end|>";

    public static string Format(ChatPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return "<|im_start|>system\n" + prompt.System + EndOfTurn + "\n" +
               "<|im_start|>user\n" + prompt.User + EndOfTurn + "\n" +
               "<|im_start|>assistant\n<think>\n\n</think>\n\n";
    }
}

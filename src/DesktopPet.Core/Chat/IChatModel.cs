namespace DesktopPet.Core.Chat;

/// <summary>What the model is asked: a system message (who the pet is) and a user message (what to say now).</summary>
public sealed record ChatPrompt(string System, string User);

/// <summary>How the model picks its words (<c>settings.json</c> → <c>Chat</c>; see <see cref="ChatRules"/>).</summary>
/// <param name="MaxTokens">Longest reply, in tokens.</param>
/// <param name="Temperature">Randomness: 0 is always the likeliest word.</param>
/// <param name="TopP">Only the likeliest words that together make up this share of the chance are considered.</param>
/// <param name="TopK">Only this many likeliest words are considered (0: no limit).</param>
/// <param name="MinP">Words less likely than this fraction of the likeliest one are dropped (0: off).</param>
/// <param name="RepeatPenalty">Above 1, words already used are less likely again (1: off).</param>
public sealed record ChatSampling(int MaxTokens, double Temperature, double TopP, int TopK, double MinP, double RepeatPenalty);

/// <summary>
/// The language model, as Core sees it. The real one (<c>DesktopPet.Chat</c>) runs a local model; tests use a fake.
/// Calls come one at a time from <see cref="ChatDirector"/>.
/// </summary>
public interface IChatModel
{
    /// <summary>Generates the raw reply to <paramref name="prompt"/>, sampled as <paramref name="sampling"/> says.</summary>
    Task<string> CompleteAsync(ChatPrompt prompt, ChatSampling sampling, CancellationToken cancellationToken);
}

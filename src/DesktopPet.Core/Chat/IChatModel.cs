namespace DesktopPet.Core.Chat;

/// <summary>What the model is asked: a system message (who the pet is) and a user message (what to say now).</summary>
public sealed record ChatPrompt(string System, string User);

/// <summary>
/// The language model, as Core sees it. The real one (<c>DesktopPet.Chat</c>) runs a local model; tests use a fake.
/// Calls come one at a time from <see cref="ChatDirector"/>.
/// </summary>
public interface IChatModel
{
    /// <summary>Generates the raw reply to <paramref name="prompt"/>, at most <paramref name="maxTokens"/> tokens long.</summary>
    Task<string> CompleteAsync(ChatPrompt prompt, int maxTokens, double temperature, CancellationToken cancellationToken);
}

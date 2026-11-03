namespace DesktopPet.Core.Abstractions;

/// <summary>Wall-clock time, injectable so stat decay and offline catch-up are testable instantly.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

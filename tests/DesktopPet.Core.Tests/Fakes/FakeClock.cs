using DesktopPet.Core.Abstractions;

namespace DesktopPet.Core.Tests.Fakes;

/// <summary>A wall clock that only moves when told to.</summary>
internal sealed class FakeClock(DateTimeOffset? start = null) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start ?? new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow += by;
}

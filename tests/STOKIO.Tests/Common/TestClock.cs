using STOKIO.Application.Abstractions;

namespace STOKIO.Tests.Common;

public sealed class TestClock(DateTimeOffset? utcNow = null) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow ?? new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
}

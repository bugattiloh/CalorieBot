using CalorieBot.Core.Time;

namespace CalorieBot.Tests.TestSupport;

/// <summary>Детерминированные часы бота для тестов — заменяют реальное время фиксированным.</summary>
public sealed class FakeDayClock : IDayClock
{
    public DateTime UtcNow { get; set; } = new(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);

    public TimeSpan Offset { get; } = TimeSpan.FromHours(3);

    public DateOnly LocalToday => DateOnly.FromDateTime(UtcNow + Offset);

    public (DateTime StartUtc, DateTime EndUtc) TodayUtcRange
    {
        get
        {
            var local = UtcNow + Offset;
            var startLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Utc);
            var startUtc = startLocal - Offset;
            return (startUtc, startUtc.AddDays(1));
        }
    }

    public TimeSpan TimeUntilReset
    {
        get
        {
            var left = TodayUtcRange.EndUtc - UtcNow;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
    }

    public DateTimeOffset ToLocal(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(Offset);
}

namespace CalorieBot.Core.Time;

/// <summary>
/// Часы бота. Вынес их за интерфейс, чтобы логику дня можно было проверить тестами
/// без привязки к реальному времени.
/// </summary>
public interface IDayClock
{
    DateTime UtcNow { get; }

    /// <summary>Смещение, в котором живёт бот (UTC+3).</summary>
    TimeSpan Offset { get; }

    /// <summary>Текущая дата в UTC+3 — «сегодня» с точки зрения пользователя.</summary>
    DateOnly LocalToday { get; }

    /// <summary>Границы текущего дня в UTC: [начало; конец). По ним фильтрую журнал.</summary>
    (DateTime StartUtc, DateTime EndUtc) TodayUtcRange { get; }

    /// <summary>Сколько осталось до сброса дневного счётчика (полночь UTC+3).</summary>
    TimeSpan TimeUntilReset { get; }

    /// <summary>Перевожу время из UTC в локальное для показа пользователю.</summary>
    DateTimeOffset ToLocal(DateTime utc);
}

/// <inheritdoc />
public sealed class DayClock : IDayClock
{
    /// <summary>
    /// Смещение задаю константой, а не таймзоной из системы: в контейнере может не быть tzdata,
    /// а мне нужен ровно UTC+3 без переходов на летнее время.
    /// </summary>
    public static readonly TimeSpan BotOffset = TimeSpan.FromHours(3);

    public DateTime UtcNow => DateTime.UtcNow;

    public TimeSpan Offset => BotOffset;

    public DateOnly LocalToday => DateOnly.FromDateTime(LocalNow.DateTime);

    public (DateTime StartUtc, DateTime EndUtc) TodayUtcRange
    {
        get
        {
            var local = LocalNow;
            var startLocal = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, BotOffset);
            return (startLocal.UtcDateTime, startLocal.AddDays(1).UtcDateTime);
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
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(BotOffset);

    private DateTimeOffset LocalNow => new DateTimeOffset(UtcNow, TimeSpan.Zero).ToOffset(BotOffset);
}

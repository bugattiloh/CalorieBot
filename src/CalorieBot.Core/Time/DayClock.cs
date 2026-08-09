namespace CalorieBot.Core.Time;

/// <summary>
/// Часы бота. Вынес их за интерфейс, чтобы время можно было проверить тестами
/// без привязки к реальным системным часам.
/// </summary>
public interface IDayClock
{
    DateTime UtcNow { get; }

    /// <summary>Смещение для отображения времени пользователю (UTC+3). Циклы КБЖУ от него не зависят.</summary>
    TimeSpan Offset { get; }

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

    public DateTimeOffset ToLocal(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(BotOffset);
}

namespace CalorieBot.Api.Infrastructure;

/// <summary>
/// Состояние поллинга. Держу его отдельным синглтоном, чтобы health check мог отвечать
/// на вопрос «бот вообще жив?» без обращения к Telegram.
/// </summary>
public sealed class BotStatus
{
    private long _handledUpdates;

    /// <summary>Поллинг запущен.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Username бота, полученный при старте через getMe.</summary>
    public string? BotUsername { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? LastUpdateAt { get; private set; }

    public DateTimeOffset? LastErrorAt { get; private set; }

    public string? LastError { get; private set; }

    public long HandledUpdates => Interlocked.Read(ref _handledUpdates);

    public void MarkStarted(string? username)
    {
        IsRunning = true;
        BotUsername = username;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void MarkStopped()
    {
        IsRunning = false;
    }

    public void MarkUpdateHandled()
    {
        Interlocked.Increment(ref _handledUpdates);
        LastUpdateAt = DateTimeOffset.UtcNow;
    }

    public void MarkError(Exception exception)
    {
        LastErrorAt = DateTimeOffset.UtcNow;
        LastError = exception.Message;
    }
}

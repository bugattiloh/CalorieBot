using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CalorieBot.Api.Infrastructure;

/// <summary>
/// Проверка живости поллинга. В Telegram специально не звоню: у getMe свои лимиты,
/// а health check вызывается часто.
/// </summary>
public sealed class TelegramBotHealthCheck : IHealthCheck
{
    /// <summary>Свежая ошибка поллинга — повод пометить сервис как degraded, но не как упавший.</summary>
    private static readonly TimeSpan RecentErrorWindow = TimeSpan.FromMinutes(1);

    private readonly BotStatus _status;

    public TelegramBotHealthCheck(BotStatus status) => _status = status;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["running"] = _status.IsRunning,
            ["botUsername"] = _status.BotUsername ?? "unknown",
            ["handledUpdates"] = _status.HandledUpdates,
            ["startedAt"] = _status.StartedAt?.ToString("O") ?? "n/a",
            ["lastUpdateAt"] = _status.LastUpdateAt?.ToString("O") ?? "n/a"
        };

        if (!_status.IsRunning)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Поллинг Telegram не запущен", data: data));
        }

        var hasRecentError = _status.LastErrorAt is { } errorAt
                             && DateTimeOffset.UtcNow - errorAt < RecentErrorWindow;

        if (hasRecentError)
        {
            data["lastError"] = _status.LastError ?? string.Empty;
            return Task.FromResult(HealthCheckResult.Degraded("Недавно была ошибка обращения к Telegram", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Поллинг работает", data));
    }
}

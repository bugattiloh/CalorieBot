using CalorieBot.Data;
using Microsoft.EntityFrameworkCore;

namespace CalorieBot.Api.Infrastructure;

/// <summary>
/// Накатываю миграции при старте. Регистрирую эту службу до бота, чтобы поллинг
/// начинался уже на готовой схеме. Postgres в compose может подниматься дольше — поэтому ретраи.
/// </summary>
public sealed class DatabaseMigratorHostedService : IHostedService
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseMigratorHostedService> _logger;

    public DatabaseMigratorHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseMigratorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CalorieBotDbContext>();

                var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

                if (pending.Count == 0)
                {
                    _logger.LogInformation("Схема базы актуальна, применять нечего");
                    return;
                }

                _logger.LogInformation("Применяю миграции: {Migrations}", string.Join(", ", pending));
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Миграции применены");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "База ещё не готова (попытка {Attempt} из {MaxAttempts}), повторю через {Delay} с",
                    attempt, MaxAttempts, RetryDelay.TotalSeconds);

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        // Все попытки исчерпаны — пусть контейнер упадёт, docker перезапустит его по restart-политике.
        throw new InvalidOperationException(
            $"Не удалось применить миграции за {MaxAttempts} попыток — проверьте доступность PostgreSQL.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

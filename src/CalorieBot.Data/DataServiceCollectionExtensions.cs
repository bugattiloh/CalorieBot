using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieBot.Data;

/// <summary>
/// Регистрация слоя данных одной строкой — чтобы Program.cs оставался читаемым.
/// </summary>
public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddCalorieBotData(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Не задана строка подключения к PostgreSQL (ConnectionStrings:Default).");
        }

        services.AddDbContext<CalorieBotDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(CalorieBotDbContext).Assembly.FullName);
                // Postgres в докере поднимается не мгновенно, поэтому включаю встроенные ретраи.
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
        });

        return services;
    }
}

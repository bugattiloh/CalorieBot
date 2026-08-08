using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CalorieBot.Data;

/// <summary>
/// Фабрика для дизайн-тайма: нужна, чтобы `dotnet ef migrations add` работал
/// без запуска всего хоста. Строку подключения беру из переменной окружения.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CalorieBotDbContext>
{
    public CalorieBotDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? Environment.GetEnvironmentVariable("CALORIEBOT_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=caloriebot;Username=caloriebot;Password=caloriebot";

        var options = new DbContextOptionsBuilder<CalorieBotDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(CalorieBotDbContext).Assembly.FullName))
            .Options;

        return new CalorieBotDbContext(options);
    }
}

using System.Text.Json;
using CalorieBot.Api.Bot;
using CalorieBot.Api.Bot.Scenarios;
using CalorieBot.Api.Configuration;
using CalorieBot.Api.Infrastructure;
using CalorieBot.Core;
using CalorieBot.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;

var builder = WebApplication.CreateBuilder(args);

// Логи пишу в JSON: так их без доработок забирает любой сборщик (docker logs → Loki/ELK).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

// Настройки бота проверяю на старте — пустой токен должен ронять приложение сразу.
builder.Services.AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddCalorieBotData(builder.Configuration.GetConnectionString("Default") ?? string.Empty);
builder.Services.AddCalorieBotCore();

// Клиент Telegram беру из HttpClientFactory — он сам следит за временем жизни соединений.
builder.Services.AddHttpClient("telegram")
    .AddTypedClient<ITelegramBotClient>((httpClient, serviceProvider) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<BotOptions>>().Value;
        return new TelegramBotClient(options.Token, httpClient);
    });

builder.Services.AddSingleton<BotStatus>();
builder.Services.AddSingleton<IUpdateHandler, BotUpdateHandler>();

// Всё, что работает с базой, живёт в scope одного апдейта.
builder.Services.AddScoped<BotMessenger>();
builder.Services.AddScoped<UpdateRouter>();
builder.Services.AddScoped<MealScenario>();
builder.Services.AddScoped<FavoritesScenario>();
builder.Services.AddScoped<DishScenario>();
builder.Services.AddScoped<LimitScenario>();
builder.Services.AddScoped<ProgressScenario>();
builder.Services.AddScoped<CycleScenario>();

// Порядок важен: сначала миграции, потом поллинг.
builder.Services.AddHostedService<DatabaseMigratorHostedService>();
builder.Services.AddHostedService<BotHostedService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CalorieBotDbContext>("postgres")
    .AddCheck<TelegramBotHealthCheck>("telegram");

// Graceful shutdown: даю приложению время догрести текущие апдейты и закрыть соединения.
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

// Короткий эндпоинт «я жив» — по нему удобно проверять контейнер руками.
app.MapGet("/", (BotStatus status) => Results.Json(new
{
    service = "CalorieBot",
    bot = status.BotUsername,
    running = status.IsRunning,
    handledUpdates = status.HandledUpdates,
    startedAt = status.StartedAt
}));

app.Run();

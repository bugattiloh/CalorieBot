using CalorieBot.Api.Bot;
using CalorieBot.Api.Bot.Scenarios;
using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Data.Entities;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CalorieBot.Tests.Scenarios;

/// <summary>Сценарии просмотра: «📊 Мой прогресс» и «📋 История сегодня».</summary>
public class ProgressScenarioTests
{
    private const long ChatId = 100;
    private const long UserId = 1;

    private sealed record Harness(ProgressScenario Scenario, RecordingBotClient Bot, IConversationStateStore States, Mock<IProgressService> Progress, Mock<IFoodLogService> FoodLog);

    private static Harness CreateHarness()
    {
        var bot = new RecordingBotClient();
        var messenger = new BotMessenger(bot.Client, NullLogger<BotMessenger>.Instance);
        var states = new MemoryConversationStateStore(new MemoryCache(new MemoryCacheOptions()));
        var progress = new Mock<IProgressService>();
        var foodLog = new Mock<IFoodLogService>();
        var clock = new FakeDayClock();

        var scenario = new ProgressScenario(messenger, progress.Object, foodLog.Object, states, clock);

        return new Harness(scenario, bot, states, progress, foodLog);
    }

    private static DailyProgress BuildProgress(int limit, int consumed) => new()
    {
        CycleStartedAt = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
        CalorieLimit = limit,
        ConsumedCalories = consumed
    };

    [Fact]
    public async Task ShowProgressAsync_SendsProgressSummary_AndResetsDialog()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingFavoriteName;
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProgress(2000, 800));

        await h.Scenario.ShowProgressAsync(ChatId, UserId, CancellationToken.None);

        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
        Assert.Contains("800", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task ShowHistoryAsync_WithEntries_ListsThemInMessage()
    {
        var h = CreateHarness();
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProgress(2000, 300));
        h.FoodLog.Setup(f => f.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLogEntry>
            {
                new() { ProductName = "Овсянка", Calories = 300, MealType = MealType.Breakfast, LoggedAt = DateTime.UtcNow }
            });

        await h.Scenario.ShowHistoryAsync(ChatId, UserId, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Contains("Овсянка", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task ShowHistoryAsync_WithNoEntries_SaysNothingLoggedYet()
    {
        var h = CreateHarness();
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProgress(2000, 0));
        h.FoodLog.Setup(f => f.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<FoodLogEntry>());

        await h.Scenario.ShowHistoryAsync(ChatId, UserId, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Contains("ещё ничего не записали", h.Bot.Sent[0].Text);
    }
}

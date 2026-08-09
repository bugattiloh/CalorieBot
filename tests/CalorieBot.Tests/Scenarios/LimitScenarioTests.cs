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

/// <summary>Сценарий «🎯 Дневной лимит»: просмотр и изменение дневного максимума калорий.</summary>
public class LimitScenarioTests
{
    private const long ChatId = 100;
    private const long UserId = 1;

    private sealed record Harness(LimitScenario Scenario, RecordingBotClient Bot, IConversationStateStore States, Mock<IUserService> Users, Mock<IProgressService> Progress);

    private static Harness CreateHarness()
    {
        var bot = new RecordingBotClient();
        var messenger = new BotMessenger(bot.Client, NullLogger<BotMessenger>.Instance);
        var states = new MemoryConversationStateStore(new MemoryCache(new MemoryCacheOptions()));
        var users = new Mock<IUserService>();
        var progress = new Mock<IProgressService>();
        var clock = new FakeDayClock();

        var scenario = new LimitScenario(messenger, users.Object, progress.Object, states, clock);

        return new Harness(scenario, bot, states, users, progress);
    }

    private static AppUser BuildUser(int limit = 2000, DateTime? goalSetAt = null) => new()
    {
        UserId = UserId,
        DailyCalorieLimit = limit,
        DailyProteinsLimit = 150,
        DailyFatsLimit = 67,
        DailyCarbsLimit = 200,
        GoalSetAt = goalSetAt
    };

    private static DailyProgress BuildProgress(int limit, int consumed) => new()
    {
        CycleStartedAt = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
        CalorieLimit = limit,
        ConsumedCalories = consumed
    };

    [Fact]
    public async Task ShowMenuAsync_SendsCurrentLimitAndResetsDialog()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingMealProductName;
        h.Users.Setup(u => u.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(1800));

        await h.Scenario.ShowMenuAsync(ChatId, UserId, CancellationToken.None);

        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
        Assert.Contains("1800", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task StartChangeAsync_AwaitsNewLimitValue()
    {
        var h = CreateHarness();
        h.Users.Setup(u => u.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser());

        await h.Scenario.StartChangeAsync(ChatId, UserId, CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingCalorieLimit, h.States.Get(UserId).State);
    }

    [Fact]
    public async Task HandleNewLimitAsync_WithValidNumber_UpdatesLimitAndReturnsToIdle()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingCalorieLimit;
        h.Users.Setup(u => u.UpdateCalorieLimitAsync(UserId, 1800, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser(1800, DateTime.UtcNow));
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProgress(1800, 400));

        await h.Scenario.HandleNewLimitAsync(ChatId, UserId, "1800", CancellationToken.None);

        h.Users.Verify(u => u.UpdateCalorieLimitAsync(UserId, 1800, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleNewLimitAsync_WithOutOfRangeNumber_DoesNotCallUpdate_AndKeepsWaiting()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingCalorieLimit;

        await h.Scenario.HandleNewLimitAsync(ChatId, UserId, "50", CancellationToken.None); // ниже минимума 500

        h.Users.Verify(u => u.UpdateCalorieLimitAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(ConversationState.AwaitingCalorieLimit, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task ShowCurrentAsync_SendsLimitAndProgressSnapshot()
    {
        var h = CreateHarness();
        h.Users.Setup(u => u.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(2000));
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProgress(2000, 500));

        await h.Scenario.ShowCurrentAsync(ChatId, UserId, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Contains("2000", h.Bot.Sent[0].Text);
    }
}

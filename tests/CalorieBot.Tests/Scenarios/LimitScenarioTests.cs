using CalorieBot.Api.Bot;
using CalorieBot.Api.Bot.Scenarios;
using CalorieBot.Core.Models;
using CalorieBot.Core.Nutrition;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Data.Entities;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Telegram.Bot.Types;

namespace CalorieBot.Tests.Scenarios;

/// <summary>Сценарий «🎯 Дневной лимит»: просмотр и изменение дневного лимита по калориям или по БЖУ.</summary>
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
        TrackingMode = CalorieTrackingMode.Calories,
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
    public async Task StartChangeAsync_AsksTrackingMode()
    {
        var h = CreateHarness();

        await h.Scenario.StartChangeAsync(ChatId, UserId, CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingLimitMode, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleLimitModeAsync_WithCalories_AwaitsCalorieLimit()
    {
        var h = CreateHarness();
        h.Users.Setup(u => u.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser());

        var query = BuildCallbackQuery("lm:cal");
        await h.Scenario.HandleLimitModeAsync(query, argument: "cal", CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingCalorieLimit, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleLimitModeAsync_WithMacro_AwaitsMacroLimits()
    {
        var h = CreateHarness();

        var query = BuildCallbackQuery("lm:macro");
        await h.Scenario.HandleLimitModeAsync(query, argument: "macro", CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingMacroLimits, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleNewMacroLimitsAsync_WithValidNumbers_UpdatesLimitsAndReturnsToIdle()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingMacroLimits;
        var macroUser = BuildUser();
        macroUser.TrackingMode = CalorieTrackingMode.Macros;
        macroUser.DailyProteinsLimit = 150;
        macroUser.DailyFatsLimit = 60;
        macroUser.DailyCarbsLimit = 250;
        h.Users.Setup(u => u.UpdateMacroLimitsAsync(UserId, 150, 60, 250, It.IsAny<CancellationToken>())).ReturnsAsync(macroUser);
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProgress(2000, 400) with { TrackingMode = CalorieTrackingMode.Macros });

        await h.Scenario.HandleNewMacroLimitsAsync(ChatId, UserId, "150 60 250", CancellationToken.None);

        h.Users.Verify(u => u.UpdateMacroLimitsAsync(UserId, 150, 60, 250, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleNewMacroLimitsAsync_WithInvalidInput_DoesNotCallUpdate()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingMacroLimits;

        await h.Scenario.HandleNewMacroLimitsAsync(ChatId, UserId, "не число", CancellationToken.None);

        h.Users.Verify(u => u.UpdateMacroLimitsAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(ConversationState.AwaitingMacroLimits, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
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

    private static CallbackQuery BuildCallbackQuery(string data) => new()
    {
        Id = "cb-1",
        From = new User { Id = UserId, FirstName = "Test" },
        Message = new Message { Id = 55, Chat = new Chat { Id = ChatId } },
        Data = data
    };

    // ------------------------------------------------------------------
    // Калькулятор нормы КБЖУ
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleLimitModeAsync_WithCalc_AwaitsSex()
    {
        var h = CreateHarness();

        var query = BuildCallbackQuery("lm:calc");
        await h.Scenario.HandleLimitModeAsync(query, argument: "calc", CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingCalcSex, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleCalcSexAsync_StoresSexAndAwaitsAge()
    {
        var h = CreateHarness();

        var query = BuildCallbackQuery("lcs:f");
        await h.Scenario.HandleCalcSexAsync(query, argument: "f", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(BodySex.Female, context.CalcSex);
        Assert.Equal(ConversationState.AwaitingCalcAge, context.State);
    }

    [Fact]
    public async Task HandleCalcAgeAsync_WithoutSexInContext_TreatsAsStaleDialog()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingCalcAge;

        await h.Scenario.HandleCalcAgeAsync(ChatId, UserId, "30", CancellationToken.None);

        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Contains(h.Bot.Sent, m => m.Text.Contains("неактуал"));
    }

    [Fact]
    public async Task FullCalculatorWizard_CollectsAllStepsAndShowsResult()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);

        await h.Scenario.HandleCalcSexAsync(BuildCallbackQuery("lcs:f"), "f", CancellationToken.None);
        Assert.Equal(ConversationState.AwaitingCalcAge, context.State);

        await h.Scenario.HandleCalcAgeAsync(ChatId, UserId, "30", CancellationToken.None);
        Assert.Equal(ConversationState.AwaitingCalcHeight, context.State);

        await h.Scenario.HandleCalcHeightAsync(ChatId, UserId, "165", CancellationToken.None);
        Assert.Equal(ConversationState.AwaitingCalcWeight, context.State);

        await h.Scenario.HandleCalcWeightAsync(ChatId, UserId, "65", CancellationToken.None);
        Assert.Equal(ConversationState.AwaitingCalcActivity, context.State);

        await h.Scenario.HandleCalcActivityAsync(BuildCallbackQuery("lca:moderate"), "moderate", CancellationToken.None);
        Assert.Equal(ConversationState.AwaitingCalcGoal, context.State);
        Assert.Equal(ActivityLevel.Moderate, context.CalcActivity);

        await h.Scenario.HandleCalcGoalAsync(BuildCallbackQuery("lcg:lose"), "lose", CancellationToken.None);

        Assert.Equal(ConversationState.Idle, context.State);
        // Вводные данные из мастера остаются в контексте до применения/отмены — иначе «Применить» не сможет пересчитать.
        Assert.Equal(BodySex.Female, context.CalcSex);
        // Редактировались: подсказка возраста (после пола), подсказка цели (после активности), карточка результата.
        Assert.Equal(3, h.Bot.Edited.Count);
        Assert.Contains("Целевая калорийность", h.Bot.Edited[^1].Text);
    }

    [Fact]
    public async Task HandleCalcApplyAsync_UpdatesMacroLimitsFromContextAndResetsDialog()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.CalcSex = BodySex.Female;
        context.CalcAge = 30;
        context.CalcHeightCm = 165;
        context.CalcWeightKg = 65m;
        context.CalcActivity = ActivityLevel.Moderate;

        h.Users.Setup(u => u.UpdateMacroLimitsAsync(UserId, 130m, 65m, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser());
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProgress(2000, 400) with { TrackingMode = CalorieTrackingMode.Macros });

        var query = BuildCallbackQuery("lcap:lose");
        await h.Scenario.HandleCalcApplyAsync(query, argument: "lose", CancellationToken.None);

        h.Users.Verify(u => u.UpdateMacroLimitsAsync(UserId, 130m, 65m, It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ConversationState.Idle, context.State);
        Assert.Null(context.CalcSex);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleCalcApplyAsync_WithMissingContext_DoesNotCallUpdate()
    {
        var h = CreateHarness(); // контекст пуст — мастер не пройден

        var query = BuildCallbackQuery("lcap:lose");
        await h.Scenario.HandleCalcApplyAsync(query, argument: "lose", CancellationToken.None);

        h.Users.Verify(
            u => u.UpdateMacroLimitsAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

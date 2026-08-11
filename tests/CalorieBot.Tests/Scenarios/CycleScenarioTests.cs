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
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CalorieBot.Tests.Scenarios;

/// <summary>Сценарий «🆕 Новый день»: закрытие текущего цикла и просмотр истории прошлых циклов.</summary>
public class CycleScenarioTests
{
    private const long ChatId = 100;
    private const long UserId = 1;

    private sealed record Harness(CycleScenario Scenario, RecordingBotClient Bot, IConversationStateStore States, Mock<ICycleService> Cycles, Mock<IProgressService> Progress);

    private static Harness CreateHarness()
    {
        var bot = new RecordingBotClient();
        var messenger = new BotMessenger(bot.Client, NullLogger<BotMessenger>.Instance);
        var states = new MemoryConversationStateStore(new MemoryCache(new MemoryCacheOptions()));
        var cycles = new Mock<ICycleService>();
        var progress = new Mock<IProgressService>();
        var clock = new FakeDayClock();

        var scenario = new CycleScenario(messenger, cycles.Object, progress.Object, states, clock);

        return new Harness(scenario, bot, states, cycles, progress);
    }

    private static DailyProgress BuildProgress(int limit, int consumed) => new()
    {
        CycleStartedAt = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc),
        TrackingMode = CalorieTrackingMode.Calories,
        CalorieLimit = limit,
        ConsumedCalories = consumed
    };

    private static CallbackQuery BuildCallbackQuery(string data) => new()
    {
        Id = "cb-1",
        From = new User { Id = UserId, FirstName = "Test" },
        Message = new Message { Id = 55, Chat = new Chat { Id = ChatId } },
        Data = data
    };

    [Fact]
    public async Task ShowNewDayConfirmAsync_SendsConfirmationWithCurrentProgress()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingCalorieLimit;
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProgress(2000, 800));

        await h.Scenario.ShowNewDayConfirmAsync(ChatId, UserId, CancellationToken.None);

        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
        Assert.Contains("800", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task HandleNewDayConfirmAsync_WithYes_StartsNewCycleAndReportsClosedOne()
    {
        var h = CreateHarness();
        var closedCycle = new CalorieCycle { UserId = UserId, ConsumedCalories = 1800, CalorieLimit = 2000 };
        h.Cycles.Setup(c => c.StartNewCycleAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(closedCycle);

        var query = BuildCallbackQuery("ndy:yes");
        await h.Scenario.HandleNewDayConfirmAsync(query, argument: "yes", CancellationToken.None);

        h.Cycles.Verify(c => c.StartNewCycleAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
        Assert.Contains("1800", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleNewDayConfirmAsync_WithNo_DoesNotStartNewCycle()
    {
        var h = CreateHarness();

        var query = BuildCallbackQuery("ndy:no");
        await h.Scenario.HandleNewDayConfirmAsync(query, argument: "no", CancellationToken.None);

        h.Cycles.Verify(c => c.StartNewCycleAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task ShowHistoryAsync_WhenEmpty_SendsEmptyHistoryMessage()
    {
        var h = CreateHarness();
        h.Cycles.Setup(c => c.GetHistoryCountAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        await h.Scenario.ShowHistoryAsync(ChatId, UserId, page: 0, editMessageId: null, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Contains("пока нет", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task ShowHistoryAsync_WithCycles_ListsThemInMessage()
    {
        var h = CreateHarness();
        h.Cycles.Setup(c => c.GetHistoryCountAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        h.Cycles.Setup(c => c.GetHistoryAsync(UserId, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CalorieCycle>
            {
                new()
                {
                    StartedAt = new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc),
                    EndedAt = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc),
                    CalorieLimit = 2000,
                    ConsumedCalories = 1950,
                    EntriesCount = 4
                }
            });

        await h.Scenario.ShowHistoryAsync(ChatId, UserId, page: 0, editMessageId: null, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Contains("1950", h.Bot.Sent[0].Text);
    }

    private static CalorieCycle BuildCycle(int id, int calories = 1000, int limit = 2000) => new()
    {
        Id = id,
        UserId = UserId,
        StartedAt = new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc),
        EndedAt = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc),
        CalorieLimit = limit,
        ConsumedCalories = calories,
        EntriesCount = 1
    };

    [Fact]
    public async Task HandleCycleDetailsAsync_WithKnownCycle_ShowsCardWithEntries()
    {
        var h = CreateHarness();
        var cycle = BuildCycle(5);
        var entry = new FoodLogEntry { Id = 1, UserId = UserId, ProductName = "Гречка", Calories = 300, MealType = MealType.Lunch, LoggedAt = cycle.EndedAt.AddHours(-1) };
        h.Cycles.Setup(c => c.GetAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(cycle);
        h.Cycles.Setup(c => c.GetEntriesAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { entry });

        var query = BuildCallbackQuery("cd:5");
        await h.Scenario.HandleCycleDetailsAsync(query, argument: "5", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Edited[0].ReplyMarkup);
        Assert.Contains(keyboard.InlineKeyboard.SelectMany(row => row), button => button.Text.Contains("Гречка"));
    }

    [Fact]
    public async Task HandleCycleDetailsAsync_WithUnknownCycle_ShowsNotFoundMessage()
    {
        var h = CreateHarness();
        h.Cycles.Setup(c => c.GetAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync((CalorieCycle?)null);

        var query = BuildCallbackQuery("cd:5");
        await h.Scenario.HandleCycleDetailsAsync(query, argument: "5", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        Assert.Contains("не найден", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleEntryDeleteRequestAsync_WithKnownEntry_AsksConfirmation()
    {
        var h = CreateHarness();
        var cycle = BuildCycle(5);
        var entry = new FoodLogEntry { Id = 7, UserId = UserId, ProductName = "Гречка", Calories = 300, MealType = MealType.Lunch, LoggedAt = cycle.EndedAt.AddHours(-1) };
        h.Cycles.Setup(c => c.GetEntriesAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { entry });

        var query = BuildCallbackQuery("cedr:5:7");
        await h.Scenario.HandleEntryDeleteRequestAsync(query, argument: "5:7", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        Assert.Contains("Удалить эту запись", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleEntryDeleteConfirmAsync_DeletesAndShowsRefreshedCard()
    {
        var h = CreateHarness();
        var cycle = BuildCycle(5, calories: 700);
        h.Cycles.Setup(c => c.DeleteEntryAsync(UserId, 5, 7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        h.Cycles.Setup(c => c.GetAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(cycle);
        h.Cycles.Setup(c => c.GetEntriesAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FoodLogEntry>());

        var query = BuildCallbackQuery("cedc:5:7");
        await h.Scenario.HandleEntryDeleteConfirmAsync(query, argument: "5:7", CancellationToken.None);

        h.Cycles.Verify(c => c.DeleteEntryAsync(UserId, 5, 7, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
        Assert.Contains("700", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task StartAddEntryAsync_WithKnownCycle_AsksForProductName()
    {
        var h = CreateHarness();
        var cycle = BuildCycle(5);
        h.Cycles.Setup(c => c.GetAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(cycle);

        var query = BuildCallbackQuery("cae:5");
        await h.Scenario.StartAddEntryAsync(query, argument: "5", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(5, context.EditingCycleId);
        Assert.Equal(ConversationState.AwaitingCycleEntryName, context.State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleEntryNameAsync_WithValidName_StoresItAndAsksMacros()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.EditingCycleId = 5;
        context.State = ConversationState.AwaitingCycleEntryName;

        await h.Scenario.HandleEntryNameAsync(ChatId, UserId, "Гречка", CancellationToken.None);

        Assert.Equal("Гречка", context.ProductName);
        Assert.Equal(ConversationState.AwaitingCycleEntryMacros, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleEntryMacrosAsync_WithValidMacros_AsksForMealType()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.EditingCycleId = 5;
        context.ProductName = "Гречка";
        context.State = ConversationState.AwaitingCycleEntryMacros;

        await h.Scenario.HandleEntryMacrosAsync(ChatId, UserId, "12 5 30", CancellationToken.None);

        Assert.Equal(213, context.Calories);
        Assert.Equal(ConversationState.Idle, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleEntryMealTypeAsync_AddsEntryAndShowsRefreshedCard()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Гречка", 12, 5, 30));

        var cycle = BuildCycle(5, calories: 213);
        h.Cycles.Setup(c => c.GetAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(cycle);
        h.Cycles.Setup(c => c.GetEntriesAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FoodLogEntry>());
        h.Cycles.Setup(c => c.AddEntryAsync(UserId, 5, It.IsAny<ProductDraft>(), MealType.Lunch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoodLogEntry { Id = 1, UserId = UserId, ProductName = "Гречка", Calories = 213, MealType = MealType.Lunch });

        var query = BuildCallbackQuery("cemt:5:2");
        await h.Scenario.HandleEntryMealTypeAsync(query, argument: "5:2", CancellationToken.None);

        h.Cycles.Verify(c => c.AddEntryAsync(UserId, 5, It.IsAny<ProductDraft>(), MealType.Lunch, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(h.States.Get(UserId).ProductName);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleEntryMealTypeAsync_WithStaleDialog_DoesNotAddEntry()
    {
        var h = CreateHarness(); // контекст пуст — ProductName не задан

        var query = BuildCallbackQuery("cemt:5:2");
        await h.Scenario.HandleEntryMealTypeAsync(query, argument: "5:2", CancellationToken.None);

        h.Cycles.Verify(c => c.AddEntryAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<ProductDraft>(), It.IsAny<MealType>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(h.Bot.AnsweredCallbackIds);
    }
}

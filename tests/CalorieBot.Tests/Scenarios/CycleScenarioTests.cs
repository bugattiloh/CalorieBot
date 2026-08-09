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
}

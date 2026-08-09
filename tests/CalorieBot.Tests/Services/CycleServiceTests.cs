using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Data.Entities;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieBot.Tests.Services;

public class CycleServiceTests
{
    private sealed record Harness(CycleService Cycles, IUserService Users, IFoodLogService FoodLog, FakeDayClock Clock);

    private static Harness CreateHarness()
    {
        var db = InMemoryDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new FakeDayClock();

        var users = new UserService(db, cache, clock, NullLogger<UserService>.Instance);
        var foodLog = new FoodLogService(db, users, clock, NullLogger<FoodLogService>.Instance);
        var cycles = new CycleService(db, users, clock, NullLogger<CycleService>.Instance);

        return new Harness(cycles, users, foodLog, clock);
    }

    [Fact]
    public async Task StartNewCycleAsync_SnapshotsClosedCycleAndMovesCycleStartToNow()
    {
        var h = CreateHarness();
        h.Clock.UtcNow = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc);
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);
        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("Завтрак", 20, 10, 30), MealType.Breakfast, null, CancellationToken.None); // 290 ккал

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 15, 0, 0, DateTimeKind.Utc);
        var closed = await h.Cycles.StartNewCycleAsync(1, CancellationToken.None);

        Assert.Equal(new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc), closed.StartedAt);
        Assert.Equal(h.Clock.UtcNow, closed.EndedAt);
        Assert.Equal(290, closed.ConsumedCalories);
        Assert.Equal(2000, closed.CalorieLimit);
        Assert.Equal(1, closed.EntriesCount);

        var user = await h.Users.GetAsync(1, CancellationToken.None);
        Assert.Equal(h.Clock.UtcNow, user.CycleStartedAt);
    }

    [Fact]
    public async Task StartNewCycleAsync_NewCycleStartsEmpty()
    {
        var h = CreateHarness();
        h.Clock.UtcNow = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc);
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);
        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("Еда", 10, 10, 10), MealType.Lunch, null, CancellationToken.None);

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 15, 0, 0, DateTimeKind.Utc);
        await h.Cycles.StartNewCycleAsync(1, CancellationToken.None);
        var afterEntries = await h.FoodLog.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.Empty(afterEntries);
    }

    [Fact]
    public async Task StartNewCycleAsync_ForUnknownUser_RecreatesProfileAndClosesEmptyCycle()
    {
        // IUserService.GetAsync сам восстанавливает пропавший профиль (например, если базу почистили
        // под работающим ботом) — CycleService намеренно наследует это поведение вместо падения с ошибкой.
        var h = CreateHarness();

        var closed = await h.Cycles.StartNewCycleAsync(999, CancellationToken.None);

        Assert.Equal(0, closed.ConsumedCalories);
        Assert.Equal(2000, closed.CalorieLimit);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsClosedCyclesNewestFirst()
    {
        var h = CreateHarness();
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);

        h.Clock.UtcNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        await h.Cycles.StartNewCycleAsync(1, CancellationToken.None); // цикл №1 закрыт 6-го

        h.Clock.UtcNow = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
        await h.Cycles.StartNewCycleAsync(1, CancellationToken.None); // цикл №2 закрыт 7-го

        var history = await h.Cycles.GetHistoryAsync(1, skip: 0, take: 10, CancellationToken.None);
        var count = await h.Cycles.GetHistoryCountAsync(1, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(2, history.Count);
        Assert.True(history[0].EndedAt > history[1].EndedAt);
    }

    [Fact]
    public async Task GetHistoryAsync_DoesNotReturnOtherUsersCycles()
    {
        var h = CreateHarness();
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);
        await h.Users.GetOrCreateAsync(2, null, null, CancellationToken.None);
        await h.Cycles.StartNewCycleAsync(2, CancellationToken.None);

        var history = await h.Cycles.GetHistoryAsync(1, skip: 0, take: 10, CancellationToken.None);

        Assert.Empty(history);
    }
}

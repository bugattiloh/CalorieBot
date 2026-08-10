using CalorieBot.Core.Services;
using CalorieBot.Data.Entities;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieBot.Tests.Services;

public class UserServiceTests
{
    private static UserService CreateService(out CalorieBot.Data.CalorieBotDbContext db, FakeDayClock? clock = null)
    {
        db = InMemoryDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new UserService(db, cache, clock ?? new FakeDayClock(), NullLogger<UserService>.Instance);
    }

    [Fact]
    public async Task GetOrCreateAsync_CreatesNewUserWithDefaultLimit()
    {
        var clock = new FakeDayClock();
        var service = CreateService(out _, clock);

        var user = await service.GetOrCreateAsync(userId: 1, username: "grig", firstName: "Grigorii", CancellationToken.None);

        Assert.Equal(2000, user.DailyCalorieLimit);
        Assert.Null(user.GoalSetAt);
        Assert.Equal("grig", user.Username);
        Assert.NotNull(user.DailyProteinsLimit);
        Assert.Equal(clock.UtcNow, user.CycleStartedAt);
    }

    [Fact]
    public async Task SetCycleStartAsync_UpdatesCycleStartAndRefreshesCache()
    {
        var service = CreateService(out _);
        await service.GetOrCreateAsync(1, null, null, CancellationToken.None);
        var newStart = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

        var updated = await service.SetCycleStartAsync(1, newStart, CancellationToken.None);

        Assert.Equal(newStart, updated.CycleStartedAt);

        // GetAsync должен сразу отдать свежее значение из кэша, а не старое.
        var cached = await service.GetAsync(1, CancellationToken.None);
        Assert.Equal(newStart, cached.CycleStartedAt);
    }

    [Fact]
    public async Task SetCycleStartAsync_Throws_WhenUserDoesNotExist()
    {
        var service = CreateService(out _);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetCycleStartAsync(userId: 999, DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsSameUser_OnSecondCall()
    {
        var service = CreateService(out var db);

        var first = await service.GetOrCreateAsync(1, "grig", "Grigorii", CancellationToken.None);
        var second = await service.GetOrCreateAsync(1, "grig", "Grigorii", CancellationToken.None);

        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(1, db.Users.Count());
    }

    [Fact]
    public async Task GetOrCreateAsync_UpdatesUsernameAndFirstName_WhenChanged()
    {
        var service = CreateService(out _);
        await service.GetOrCreateAsync(1, "old_name", "Old", CancellationToken.None);

        var updated = await service.GetOrCreateAsync(1, "new_name", "New", CancellationToken.None);

        Assert.Equal("new_name", updated.Username);
        Assert.Equal("New", updated.FirstName);
    }

    [Fact]
    public async Task UpdateCalorieLimitAsync_ChangesLimitAndRecalculatesMacrosAndGoalSetAt()
    {
        var clock = new FakeDayClock();
        var service = CreateService(out _, clock);
        await service.GetOrCreateAsync(1, null, null, CancellationToken.None);

        var updated = await service.UpdateCalorieLimitAsync(1, 1800, CancellationToken.None);

        Assert.Equal(1800, updated.DailyCalorieLimit);
        Assert.Equal(clock.UtcNow, updated.GoalSetAt);
        Assert.NotNull(updated.DailyProteinsLimit);
    }

    [Fact]
    public async Task UpdateCalorieLimitAsync_Throws_WhenUserDoesNotExist()
    {
        var service = CreateService(out _);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateCalorieLimitAsync(userId: 999, newLimit: 1800, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateMacroLimitsAsync_SetsExactMacrosAndSwitchesMode()
    {
        var service = CreateService(out _);
        await service.GetOrCreateAsync(1, null, null, CancellationToken.None);

        var updated = await service.UpdateMacroLimitsAsync(1, proteins: 150, fats: 60, carbs: 250, CancellationToken.None);

        Assert.Equal(CalorieTrackingMode.Macros, updated.TrackingMode);
        Assert.Equal(150, updated.DailyProteinsLimit);
        Assert.Equal(60, updated.DailyFatsLimit);
        Assert.Equal(250, updated.DailyCarbsLimit);
        // 150*4 + 60*9 + 250*4 = 600 + 540 + 1000 = 2140
        Assert.Equal(2140, updated.DailyCalorieLimit);
    }

    [Fact]
    public async Task UpdateCalorieLimitAsync_SwitchesModeBackToCalories_AfterMacroMode()
    {
        var service = CreateService(out _);
        await service.GetOrCreateAsync(1, null, null, CancellationToken.None);
        await service.UpdateMacroLimitsAsync(1, 150, 60, 250, CancellationToken.None);

        var updated = await service.UpdateCalorieLimitAsync(1, 1800, CancellationToken.None);

        Assert.Equal(CalorieTrackingMode.Calories, updated.TrackingMode);
        Assert.Equal(1800, updated.DailyCalorieLimit);
    }

    [Fact]
    public async Task GetAsync_RecreatesProfile_WhenMissingFromDatabase()
    {
        var service = CreateService(out _);

        var user = await service.GetAsync(userId: 42, CancellationToken.None);

        Assert.Equal(42, user.UserId);
        Assert.Equal(2000, user.DailyCalorieLimit);
    }
}

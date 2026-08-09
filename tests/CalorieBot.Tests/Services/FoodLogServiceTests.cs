using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Data.Entities;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieBot.Tests.Services;

public class FoodLogServiceTests
{
    private sealed record Harness(FoodLogService FoodLog, IUserService Users, FakeDayClock Clock);

    private static Harness CreateHarness()
    {
        var db = InMemoryDbContextFactory.Create();
        var clock = new FakeDayClock();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var users = new UserService(db, cache, clock, NullLogger<UserService>.Instance);
        var foodLog = new FoodLogService(db, users, clock, NullLogger<FoodLogService>.Instance);

        return new Harness(foodLog, users, clock);
    }

    [Fact]
    public async Task LogAsync_StoresEntryWithMacrosAndTimestampFromClock()
    {
        var h = CreateHarness();
        var draft = ProductDraft.FromMacros("Банан", proteins: 1, fats: 0, carbs: 27);

        var entry = await h.FoodLog.LogAsync(userId: 1, draft, MealType.Snack, favoriteProductId: null, CancellationToken.None);

        Assert.Equal("Банан", entry.ProductName);
        Assert.Equal(draft.Calories, entry.Calories);
        Assert.Equal(MealType.Snack, entry.MealType);
        Assert.Equal(h.Clock.UtcNow, entry.LoggedAt);
        Assert.False(entry.IsFavorite);
        Assert.Null(entry.FavoriteProductId);
    }

    [Fact]
    public async Task LogAsync_MarksEntryAsFavorite_WhenFavoriteIdProvided()
    {
        var h = CreateHarness();
        var draft = ProductDraft.FromMacros("Гречка", proteins: 12, fats: 3, carbs: 60);

        var entry = await h.FoodLog.LogAsync(1, draft, MealType.Lunch, favoriteProductId: 7, CancellationToken.None);

        Assert.True(entry.IsFavorite);
        Assert.Equal(7, entry.FavoriteProductId);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_ExcludesEntriesLoggedBeforeCycleStart()
    {
        var h = CreateHarness();
        var draft = ProductDraft.FromMacros("Яблоко", proteins: 0, fats: 0, carbs: 25);

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc);
        await h.FoodLog.LogAsync(1, draft, MealType.Snack, null, CancellationToken.None); // до начала цикла

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None); // цикл начинается сейчас

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 11, 0, 0, DateTimeKind.Utc);
        await h.FoodLog.LogAsync(1, draft, MealType.Snack, null, CancellationToken.None); // уже в цикле

        var current = await h.FoodLog.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.Single(current);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_ReturnsEntriesOrderedByLoggedAt()
    {
        var h = CreateHarness();
        var draft = ProductDraft.FromMacros("Тест", proteins: 1, fats: 1, carbs: 1);

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 8, 0, 0, DateTimeKind.Utc);
        await h.FoodLog.LogAsync(1, draft with { Name = "Второй завтрак" }, MealType.Breakfast, null, CancellationToken.None);

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 6, 0, 0, DateTimeKind.Utc);
        await h.FoodLog.LogAsync(1, draft with { Name = "Первый завтрак" }, MealType.Breakfast, null, CancellationToken.None);

        var current = await h.FoodLog.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.Equal(2, current.Count);
        Assert.Equal("Первый завтрак", current[0].ProductName);
        Assert.Equal("Второй завтрак", current[1].ProductName);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_DoesNotReturnOtherUsersEntries()
    {
        var h = CreateHarness();
        var draft = ProductDraft.FromMacros("Чужое", proteins: 1, fats: 1, carbs: 1);
        await h.FoodLog.LogAsync(userId: 2, draft, MealType.Lunch, null, CancellationToken.None);

        var current = await h.FoodLog.GetCurrentCycleAsync(userId: 1, CancellationToken.None);

        Assert.Empty(current);
    }
}

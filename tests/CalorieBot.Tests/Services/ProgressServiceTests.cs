using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Data.Entities;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieBot.Tests.Services;

/// <summary>
/// Прогресс дня складывается из нескольких сервисов сразу (пользователь, дневник, избранное) —
/// поэтому здесь они собраны как в реальном сценарии «📊 Мой прогресс», а не замоканы по отдельности.
/// </summary>
public class ProgressServiceTests
{
    private sealed record Harness(
        ProgressService Progress,
        IUserService Users,
        IFavoriteProductService Favorites,
        IFoodLogService FoodLog,
        FakeDayClock Clock);

    private static Harness CreateHarness()
    {
        var db = InMemoryDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new FakeDayClock();

        var users = new UserService(db, cache, clock, NullLogger<UserService>.Instance);
        var favorites = new FavoriteProductService(db, cache, clock, NullLogger<FavoriteProductService>.Instance);
        var foodLog = new FoodLogService(db, users, clock, NullLogger<FoodLogService>.Instance);
        var progress = new ProgressService(db, users, favorites, clock);

        return new Harness(progress, users, favorites, foodLog, clock);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_WithNoEntries_ReportsFullLimitRemaining()
    {
        var h = CreateHarness();
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);

        var progress = await h.Progress.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.Equal(2000, progress.CalorieLimit);
        Assert.Equal(0, progress.ConsumedCalories);
        Assert.Equal(2000, progress.RemainingCalories);
        Assert.False(progress.IsExceeded);
        Assert.Equal(0, progress.EntriesCount);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_SumsCaloriesAndMacrosAcrossEntries()
    {
        var h = CreateHarness();
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);

        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("Овсянка", 10, 5, 40), MealType.Breakfast, null, CancellationToken.None);
        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("Курица", 30, 10, 0), MealType.Lunch, null, CancellationToken.None);

        var progress = await h.Progress.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.Equal(2, progress.EntriesCount);
        Assert.Equal(40, progress.Proteins);
        Assert.Equal(15, progress.Fats);
        Assert.Equal(40, progress.Carbs);
        Assert.Equal(progress.ConsumedCalories, progress.CalorieLimit - progress.RemainingCalories);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_ReportsExceeded_WhenConsumedPastLimit()
    {
        var h = CreateHarness();
        var user = await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);
        await h.Users.UpdateCalorieLimitAsync(1, 500, CancellationToken.None);

        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("Плотный обед", 50, 30, 60), MealType.Lunch, null, CancellationToken.None);

        var progress = await h.Progress.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.True(progress.IsExceeded);
        Assert.Equal(0, progress.RemainingCalories);
        Assert.True(progress.ExceededBy > 0);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_FittingFavorites_OnlyIncludesProductsWithinRemainingCalories()
    {
        var h = CreateHarness();
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None); // лимит 2000

        await h.Favorites.AddOrUpdateAsync(1, ProductDraft.FromMacros("Лёгкий перекус", 5, 2, 10), CancellationToken.None); // ~78 ккал
        await h.Favorites.AddOrUpdateAsync(1, ProductDraft.FromMacros("Огромный бургер", 100, 100, 200), CancellationToken.None); // намного больше остатка

        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("Обед", 100, 50, 100), MealType.Lunch, null, CancellationToken.None); // ~1250 ккал, остаток < 1000

        var progress = await h.Progress.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.Contains(progress.FittingFavorites, p => p.Name == "Лёгкий перекус");
        Assert.DoesNotContain(progress.FittingFavorites, p => p.Name == "Огромный бургер");
    }

    [Fact]
    public async Task GetCurrentCycleAsync_FittingFavorites_IsEmpty_WhenLimitAlreadyExceeded()
    {
        var h = CreateHarness();
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);
        await h.Users.UpdateCalorieLimitAsync(1, 500, CancellationToken.None);
        await h.Favorites.AddOrUpdateAsync(1, ProductDraft.FromMacros("Дешёвый снек", 1, 1, 1), CancellationToken.None); // ~13 ккал

        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("Много еды", 50, 30, 60), MealType.Lunch, null, CancellationToken.None);

        var progress = await h.Progress.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.True(progress.IsExceeded);
        Assert.Empty(progress.FittingFavorites);
    }

    [Fact]
    public async Task GetCurrentCycleAsync_ExcludesEntriesFromBeforeCycleStart()
    {
        var h = CreateHarness();

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc);
        await h.Users.GetOrCreateAsync(1, null, null, CancellationToken.None);
        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("До сброса", 10, 10, 10), MealType.Lunch, null, CancellationToken.None); // 170 ккал

        h.Clock.UtcNow = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        await h.Users.SetCycleStartAsync(1, h.Clock.UtcNow, CancellationToken.None); // имитирую «Новый день»
        await h.FoodLog.LogAsync(1, ProductDraft.FromMacros("После сброса", 5, 0, 0), MealType.Snack, null, CancellationToken.None); // 20 ккал

        var progress = await h.Progress.GetCurrentCycleAsync(1, CancellationToken.None);

        Assert.Equal(1, progress.EntriesCount);
        Assert.Equal(20, progress.ConsumedCalories);
    }

    [Fact]
    public async Task GetFittingFavoritesAsync_OrdersByCaloriesDescendingThenName()
    {
        var h = CreateHarness();
        await h.Favorites.AddOrUpdateAsync(1, ProductDraft.FromMacros("Б продукт", 10, 0, 0), CancellationToken.None); // 40 ккал
        await h.Favorites.AddOrUpdateAsync(1, ProductDraft.FromMacros("А продукт", 10, 0, 0), CancellationToken.None); // 40 ккал, тот же лимит
        await h.Favorites.AddOrUpdateAsync(1, ProductDraft.FromMacros("В продукт", 20, 0, 0), CancellationToken.None); // 80 ккал

        var fitting = await h.Progress.GetFittingFavoritesAsync(1, remainingCalories: 100, CancellationToken.None);

        Assert.Equal(new[] { "В продукт", "А продукт", "Б продукт" }, fitting.Select(p => p.Name));
    }
}

using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieBot.Tests.Services;

public class FavoriteProductServiceTests
{
    private static FavoriteProductService CreateService(out CalorieBot.Data.CalorieBotDbContext db)
    {
        db = InMemoryDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new FavoriteProductService(db, cache, new FakeDayClock(), NullLogger<FavoriteProductService>.Instance);
    }

    [Fact]
    public async Task AddOrUpdateAsync_CreatesNewFavorite_WhenNameIsNew()
    {
        var service = CreateService(out _);
        var draft = ProductDraft.FromMacros("Гречка", proteins: 12, fats: 3, carbs: 60, servingSize: "150 г");

        var (created, product) = await service.AddOrUpdateAsync(userId: 1, draft, isFixedServing: true, CancellationToken.None);

        Assert.True(created);
        Assert.Equal("Гречка", product.Name);
        Assert.Equal(draft.Calories, product.Calories);
    }

    [Fact]
    public async Task AddOrUpdateAsync_UpdatesExistingFavorite_WhenNameAlreadyExistsForUser()
    {
        var service = CreateService(out _);
        var original = ProductDraft.FromMacros("Гречка", proteins: 12, fats: 3, carbs: 60);
        var (_, firstSave) = await service.AddOrUpdateAsync(1, original, isFixedServing: true, CancellationToken.None);

        var revised = ProductDraft.FromMacros("Гречка", proteins: 15, fats: 4, carbs: 65);
        var (created, updated) = await service.AddOrUpdateAsync(1, revised, isFixedServing: true, CancellationToken.None);

        Assert.False(created);
        Assert.Equal(firstSave.Id, updated.Id);
        Assert.Equal(revised.Calories, updated.Calories);
        Assert.Equal(15, updated.Proteins);
    }

    [Fact]
    public async Task AddOrUpdateAsync_SameNameForDifferentUsers_CreatesSeparateFavorites()
    {
        var service = CreateService(out _);
        var draft = ProductDraft.FromMacros("Гречка", proteins: 12, fats: 3, carbs: 60);

        var (_, userOneProduct) = await service.AddOrUpdateAsync(userId: 1, draft, isFixedServing: true, CancellationToken.None);
        var (created, userTwoProduct) = await service.AddOrUpdateAsync(userId: 2, draft, isFixedServing: true, CancellationToken.None);

        Assert.True(created);
        Assert.NotEqual(userOneProduct.Id, userTwoProduct.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyThisUsersFavorites_SortedByName()
    {
        var service = CreateService(out _);
        await service.AddOrUpdateAsync(1, ProductDraft.FromMacros("Рис", 7, 1, 78), isFixedServing: true, CancellationToken.None);
        await service.AddOrUpdateAsync(1, ProductDraft.FromMacros("Авокадо", 2, 15, 9), isFixedServing: true, CancellationToken.None);
        await service.AddOrUpdateAsync(2, ProductDraft.FromMacros("Чужой продукт", 1, 1, 1), isFixedServing: true, CancellationToken.None);

        var favorites = await service.GetAllAsync(userId: 1, CancellationToken.None);

        Assert.Equal(2, favorites.Count);
        Assert.Equal("Авокадо", favorites[0].Name);
        Assert.Equal("Рис", favorites[1].Name);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenFavoriteBelongsToAnotherUser()
    {
        var service = CreateService(out _);
        var (_, product) = await service.AddOrUpdateAsync(1, ProductDraft.FromMacros("Рис", 7, 1, 78), isFixedServing: true, CancellationToken.None);

        var result = await service.GetAsync(userId: 2, product.Id, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFavorite_AndReturnsNullOnSecondDelete()
    {
        var service = CreateService(out _);
        var (_, product) = await service.AddOrUpdateAsync(1, ProductDraft.FromMacros("Рис", 7, 1, 78), isFixedServing: true, CancellationToken.None);

        var deleted = await service.DeleteAsync(1, product.Id, CancellationToken.None);
        var deletedAgain = await service.DeleteAsync(1, product.Id, CancellationToken.None);

        Assert.NotNull(deleted);
        Assert.Null(deletedAgain);
        Assert.Empty(await service.GetAllAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task AddOrUpdateAsync_StoresIsFixedServing_OnCreate()
    {
        var service = CreateService(out _);
        var draft = ProductDraft.FromMacros("Рис", 7, 1, 78);

        var (_, product) = await service.AddOrUpdateAsync(1, draft, isFixedServing: false, CancellationToken.None);

        Assert.False(product.IsFixedServing);
    }

    [Fact]
    public async Task AddOrUpdateAsync_UpdatesIsFixedServing_OnExistingFavorite()
    {
        var service = CreateService(out _);
        var draft = ProductDraft.FromMacros("Рис", 7, 1, 78);
        await service.AddOrUpdateAsync(1, draft, isFixedServing: false, CancellationToken.None);

        var (created, updated) = await service.AddOrUpdateAsync(1, draft, isFixedServing: true, CancellationToken.None);

        Assert.False(created);
        Assert.True(updated.IsFixedServing);
    }

    [Fact]
    public async Task SetFixedServingAsync_TogglesFlag_WithoutChangingMacros()
    {
        var service = CreateService(out _);
        var draft = ProductDraft.FromMacros("Рис", 7, 1, 78);
        var (_, product) = await service.AddOrUpdateAsync(1, draft, isFixedServing: true, CancellationToken.None);

        var updated = await service.SetFixedServingAsync(1, product.Id, isFixedServing: false, CancellationToken.None);

        Assert.False(updated.IsFixedServing);
        Assert.Equal(product.Calories, updated.Calories);
    }

    [Fact]
    public async Task SetFixedServingAsync_Throws_WhenProductDoesNotExist()
    {
        var service = CreateService(out _);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetFixedServingAsync(1, favoriteId: 999, isFixedServing: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetAllAsync_ReflectsChanges_AfterCacheInvalidatingWrite()
    {
        var service = CreateService(out _);
        await service.AddOrUpdateAsync(1, ProductDraft.FromMacros("Рис", 7, 1, 78), isFixedServing: true, CancellationToken.None);
        var firstRead = await service.GetAllAsync(1, CancellationToken.None); // прогреваю кэш

        await service.AddOrUpdateAsync(1, ProductDraft.FromMacros("Гречка", 12, 3, 60), isFixedServing: true, CancellationToken.None);
        var secondRead = await service.GetAllAsync(1, CancellationToken.None);

        Assert.Single(firstRead);
        Assert.Equal(2, secondRead.Count);
    }
}

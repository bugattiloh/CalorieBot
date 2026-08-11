using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Data.Entities;
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

    // ------------------------------------------------------------------
    // Категории «Продуктов»
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetProductCategoriesAsync_SeedsFourBuiltInCategories_OnFirstCall()
    {
        var service = CreateService(out _);

        var categories = await service.GetProductCategoriesAsync(1, CancellationToken.None);

        Assert.Equal(4, categories.Count);
        Assert.All(categories, c => Assert.True(c.IsBuiltIn));
    }

    [Fact]
    public async Task GetProductCategoriesAsync_DoesNotReseed_OnSecondCall()
    {
        var service = CreateService(out _);
        await service.GetProductCategoriesAsync(1, CancellationToken.None);

        var categories = await service.GetProductCategoriesAsync(1, CancellationToken.None);

        Assert.Equal(4, categories.Count);
    }

    [Fact]
    public async Task CreateProductCategoryAsync_AddsCustomCategory_NotMarkedBuiltIn()
    {
        var service = CreateService(out _);

        var category = await service.CreateProductCategoryAsync(1, "Напитки", CancellationToken.None);

        Assert.False(category.IsBuiltIn);
        Assert.Equal("Напитки", category.Name);
    }

    [Fact]
    public async Task RenameProductCategoryAsync_UpdatesName()
    {
        var service = CreateService(out _);
        var category = await service.CreateProductCategoryAsync(1, "Старое имя", CancellationToken.None);

        var renamed = await service.RenameProductCategoryAsync(1, category.Id, "Новое имя", CancellationToken.None);

        Assert.NotNull(renamed);
        Assert.Equal("Новое имя", renamed!.Name);
    }

    [Fact]
    public async Task RenameProductCategoryAsync_WithOtherUsersCategory_ReturnsNull()
    {
        var service = CreateService(out _);
        var category = await service.CreateProductCategoryAsync(1, "Категория", CancellationToken.None);

        var renamed = await service.RenameProductCategoryAsync(2, category.Id, "Другое имя", CancellationToken.None);

        Assert.Null(renamed);
    }

    [Fact]
    public async Task DeleteProductCategoryAsync_ReassignsProductsToUncategorized_InsteadOfDeletingThem()
    {
        var service = CreateService(out _);
        var category = await service.CreateProductCategoryAsync(1, "Крупы", CancellationToken.None);
        var draft = ProductDraft.FromMacros("Гречка", 12, 3, 60);
        var (_, product) = await service.AddOrUpdateAsync(
            1, draft, isFixedServing: true, CancellationToken.None, FavoriteCategoryKind.Product, category.Id);

        var deleted = await service.DeleteProductCategoryAsync(1, category.Id, CancellationToken.None);

        Assert.True(deleted);
        var refreshed = await service.GetAsync(1, product.Id, CancellationToken.None);
        Assert.NotNull(refreshed);
        Assert.Null(refreshed!.ProductCategoryId);
    }

    [Fact]
    public async Task SetProductCategoryAsync_MovesProductToAnotherCategory()
    {
        var service = CreateService(out _);
        var categoryA = await service.CreateProductCategoryAsync(1, "А", CancellationToken.None);
        var categoryB = await service.CreateProductCategoryAsync(1, "Б", CancellationToken.None);
        var (_, product) = await service.AddOrUpdateAsync(
            1, ProductDraft.FromMacros("Рис", 7, 1, 78), isFixedServing: true, CancellationToken.None, FavoriteCategoryKind.Product, categoryA.Id);

        var moved = await service.SetProductCategoryAsync(1, product.Id, categoryB.Id, CancellationToken.None);

        Assert.NotNull(moved);
        Assert.Equal(categoryB.Id, moved!.ProductCategoryId);
    }

    [Fact]
    public async Task GetByCategoryAsync_FiltersByKindAndSubcategory()
    {
        var service = CreateService(out _);
        var category = await service.CreateProductCategoryAsync(1, "Крупы", CancellationToken.None);
        await service.AddOrUpdateAsync(
            1, ProductDraft.FromMacros("Гречка", 12, 3, 60), isFixedServing: true, CancellationToken.None, FavoriteCategoryKind.Product, category.Id);
        await service.AddOrUpdateAsync(
            1, ProductDraft.FromMacros("Рис", 7, 1, 78), isFixedServing: true, CancellationToken.None, FavoriteCategoryKind.Product, null);
        await service.EnsureWaterSeedAsync(1, CancellationToken.None);

        var inCategory = await service.GetByCategoryAsync(1, FavoriteCategoryKind.Product, category.Id, CancellationToken.None);
        var uncategorized = await service.GetByCategoryAsync(1, FavoriteCategoryKind.Product, null, CancellationToken.None);
        var water = await service.GetByCategoryAsync(1, FavoriteCategoryKind.Water, null, CancellationToken.None);

        Assert.Single(inCategory);
        Assert.Equal("Гречка", inCategory[0].Name);
        Assert.Single(uncategorized);
        Assert.Equal("Рис", uncategorized[0].Name);
        Assert.Single(water);
        Assert.Equal("Вода", water[0].Name);
    }

    // ------------------------------------------------------------------
    // Вода
    // ------------------------------------------------------------------

    [Fact]
    public async Task EnsureWaterSeedAsync_CreatesEmptyWaterItem_OnFirstCall()
    {
        var service = CreateService(out _);

        await service.EnsureWaterSeedAsync(1, CancellationToken.None);

        var water = await service.GetByCategoryAsync(1, FavoriteCategoryKind.Water, null, CancellationToken.None);
        Assert.Single(water);
        Assert.Equal("Вода", water[0].Name);
        Assert.Equal(0, water[0].Calories);
        Assert.False(water[0].IsFixedServing);
    }

    [Fact]
    public async Task EnsureWaterSeedAsync_DoesNotDuplicate_OnSecondCall()
    {
        var service = CreateService(out _);
        await service.EnsureWaterSeedAsync(1, CancellationToken.None);

        await service.EnsureWaterSeedAsync(1, CancellationToken.None);

        var water = await service.GetByCategoryAsync(1, FavoriteCategoryKind.Water, null, CancellationToken.None);
        Assert.Single(water);
    }

    // ------------------------------------------------------------------
    // Готовые блюда
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateDishAsync_CreatesFixedServingDishWithZeroMacros()
    {
        var service = CreateService(out _);

        var dish = await service.CreateDishAsync(1, "Овсянка с бананом", CancellationToken.None);

        Assert.Equal(FavoriteCategoryKind.Dish, dish.CategoryKind);
        Assert.True(dish.IsFixedServing);
        Assert.Equal(0, dish.Calories);
    }

    [Fact]
    public async Task AddDishIngredientAsync_RecomputesDishTotals_AsSumOfIngredients()
    {
        var service = CreateService(out _);
        var dish = await service.CreateDishAsync(1, "Овсянка с бананом", CancellationToken.None);

        await service.AddDishIngredientAsync(1, dish.Id, ProductDraft.FromMacros("Овсянка", 10, 5, 40), CancellationToken.None);
        var updated = await service.AddDishIngredientAsync(1, dish.Id, ProductDraft.FromMacros("Банан", 1, 0, 27), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(11, updated!.Proteins);
        Assert.Equal(5, updated.Fats);
        Assert.Equal(67, updated.Carbs);

        var ingredients = await service.GetDishIngredientsAsync(1, dish.Id, CancellationToken.None);
        Assert.Equal(2, ingredients.Count);
    }

    [Fact]
    public async Task RemoveDishIngredientAsync_RecomputesDishTotals_AfterRemoval()
    {
        var service = CreateService(out _);
        var dish = await service.CreateDishAsync(1, "Салат", CancellationToken.None);
        await service.AddDishIngredientAsync(1, dish.Id, ProductDraft.FromMacros("Огурец", 1, 0, 3), CancellationToken.None);
        var afterSecond = await service.AddDishIngredientAsync(1, dish.Id, ProductDraft.FromMacros("Масло", 0, 10, 0), CancellationToken.None);

        // EF трекает FavoriteProduct по Id — afterSecond и updated окажутся одним и тем же объектом,
        // поэтому калорийность «до удаления» фиксирую в отдельную переменную заранее.
        var caloriesBeforeRemoval = afterSecond!.Calories;

        var ingredients = await service.GetDishIngredientsAsync(1, dish.Id, CancellationToken.None);
        var oilIngredient = ingredients.Single(i => i.Name == "Масло");

        var updated = await service.RemoveDishIngredientAsync(1, dish.Id, oilIngredient.Id, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(0, updated!.Fats);
        Assert.True(updated.Calories < caloriesBeforeRemoval);
    }

    [Fact]
    public async Task AddDishIngredientAsync_WithOtherUsersDish_ReturnsNull()
    {
        var service = CreateService(out _);
        var dish = await service.CreateDishAsync(1, "Салат", CancellationToken.None);

        var result = await service.AddDishIngredientAsync(2, dish.Id, ProductDraft.FromMacros("Огурец", 1, 0, 3), CancellationToken.None);

        Assert.Null(result);
    }
}

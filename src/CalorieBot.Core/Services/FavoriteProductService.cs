using CalorieBot.Core.Models;
using CalorieBot.Core.Time;
using CalorieBot.Data;
using CalorieBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CalorieBot.Core.Services;

/// <summary>Избранные продукты пользователя.</summary>
public interface IFavoriteProductService
{
    /// <summary>Весь список избранного, отсортированный по названию (кэшируется).</summary>
    Task<IReadOnlyList<FavoriteProduct>> GetAllAsync(long userId, CancellationToken ct);

    /// <summary>Один продукт по Id — с проверкой, что он принадлежит этому пользователю.</summary>
    Task<FavoriteProduct?> GetAsync(long userId, int favoriteId, CancellationToken ct);

    /// <summary>
    /// Добавляю продукт в избранное. Если продукт с таким названием уже есть, обновляю его КБЖУ и тип порции
    /// и возвращаю Created = false — так пользователь не получит ошибку из-за дубля.
    /// </summary>
    Task<(bool Created, FavoriteProduct Product)> AddOrUpdateAsync(
        long userId,
        ProductDraft draft,
        bool isFixedServing,
        CancellationToken ct,
        FavoriteCategoryKind categoryKind = FavoriteCategoryKind.Product,
        int? productCategoryId = null);

    /// <summary>Переключаю тип порции без изменения КБЖУ — для отдельной кнопки в карточке продукта.</summary>
    Task<FavoriteProduct> SetFixedServingAsync(long userId, int favoriteId, bool isFixedServing, CancellationToken ct);

    /// <summary>Удаляю продукт из избранного. Записи журнала при этом остаются.</summary>
    Task<FavoriteProduct?> DeleteAsync(long userId, int favoriteId, CancellationToken ct);

    /// <summary>Только продукты выбранной группы (и подкатегории, если это «Продукты»), по названию.</summary>
    Task<IReadOnlyList<FavoriteProduct>> GetByCategoryAsync(
        long userId, FavoriteCategoryKind kind, int? productCategoryId, CancellationToken ct);

    /// <summary>Подкатегории «Продуктов» пользователя. На первое обращение сам заводит 4 базовые.</summary>
    Task<IReadOnlyList<ProductCategory>> GetProductCategoriesAsync(long userId, CancellationToken ct);

    /// <summary>Новая пользовательская подкатегория «Продуктов».</summary>
    Task<ProductCategory> CreateProductCategoryAsync(long userId, string name, CancellationToken ct);

    /// <summary>Переименовываю подкатегорию — в том числе одну из базовых.</summary>
    Task<ProductCategory?> RenameProductCategoryAsync(long userId, int categoryId, string name, CancellationToken ct);

    /// <summary>
    /// Удаляю подкатегорию. Продукты внутри неё не удаляются — становятся «без подкатегории»,
    /// чтобы удаление категории не превращалось в удаление избранного.
    /// </summary>
    Task<bool> DeleteProductCategoryAsync(long userId, int categoryId, CancellationToken ct);

    /// <summary>«Перетаскиваю» продукт в другую подкатегорию «Продуктов» (или убираю подкатегорию, передав null).</summary>
    Task<FavoriteProduct?> SetProductCategoryAsync(long userId, int favoriteId, int? productCategoryId, CancellationToken ct);

    /// <summary>
    /// Гарантирую, что в «Воде» пользователя есть хотя бы один пустой (0/0/0/0) элемент — завожу лениво
    /// при первом обращении к разделу, а не при регистрации, чтобы не плодить его тем, кто водой не пользуется.
    /// </summary>
    Task EnsureWaterSeedAsync(long userId, CancellationToken ct);

    /// <summary>Завожу пустое «Готовое блюдо» — КБЖУ пока нулевые, наполняются ингредиентами.</summary>
    Task<FavoriteProduct> CreateDishAsync(long userId, string name, CancellationToken ct);

    /// <summary>Ингредиенты блюда.</summary>
    Task<IReadOnlyList<DishIngredient>> GetDishIngredientsAsync(long userId, int dishId, CancellationToken ct);

    /// <summary>Добавляю ингредиент и пересчитываю итоговое КБЖУ блюда автосуммой.</summary>
    Task<FavoriteProduct?> AddDishIngredientAsync(long userId, int dishId, ProductDraft ingredient, CancellationToken ct);

    /// <summary>Убираю ингредиент и пересчитываю итоговое КБЖУ блюда.</summary>
    Task<FavoriteProduct?> RemoveDishIngredientAsync(long userId, int dishId, int ingredientId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class FavoriteProductService : IFavoriteProductService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly CalorieBotDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IDayClock _clock;
    private readonly ILogger<FavoriteProductService> _logger;

    public FavoriteProductService(
        CalorieBotDbContext db,
        IMemoryCache cache,
        IDayClock clock,
        ILogger<FavoriteProductService> logger)
    {
        _db = db;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FavoriteProduct>> GetAllAsync(long userId, CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey(userId), out IReadOnlyList<FavoriteProduct>? cached) && cached is not null)
        {
            return cached;
        }

        // Читаю без трекинга: список только показываю, менять его через эти объекты не собираюсь.
        var products = await _db.FavoriteProducts
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        _cache.Set(CacheKey(userId), (IReadOnlyList<FavoriteProduct>)products, CacheLifetime);
        return products;
    }

    public async Task<FavoriteProduct?> GetAsync(long userId, int favoriteId, CancellationToken ct)
    {
        // Сначала пробую взять из уже загруженного списка, чтобы не ходить в базу на каждый тап.
        var cached = await GetAllAsync(userId, ct);
        return cached.FirstOrDefault(p => p.Id == favoriteId);
    }

    public async Task<(bool Created, FavoriteProduct Product)> AddOrUpdateAsync(
        long userId,
        ProductDraft draft,
        bool isFixedServing,
        CancellationToken ct,
        FavoriteCategoryKind categoryKind = FavoriteCategoryKind.Product,
        int? productCategoryId = null)
    {
        var existing = await _db.FavoriteProducts
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Name == draft.Name, ct);

        if (existing is not null)
        {
            existing.Calories = draft.Calories;
            existing.Proteins = draft.Proteins;
            existing.Fats = draft.Fats;
            existing.Carbs = draft.Carbs;
            existing.IsFixedServing = isFixedServing;
            existing.ServingSize = draft.ServingSize ?? existing.ServingSize;
            existing.CategoryKind = categoryKind;
            existing.ProductCategoryId = categoryKind == FavoriteCategoryKind.Product ? productCategoryId : null;

            await _db.SaveChangesAsync(ct);
            Invalidate(userId);
            _logger.LogInformation("Обновил избранный продукт {ProductId} пользователя {UserId}", existing.Id, userId);

            return (false, existing);
        }

        var product = new FavoriteProduct
        {
            UserId = userId,
            Name = draft.Name,
            Calories = draft.Calories,
            Proteins = draft.Proteins,
            Fats = draft.Fats,
            Carbs = draft.Carbs,
            IsFixedServing = isFixedServing,
            ServingSize = draft.ServingSize,
            CreatedAt = _clock.UtcNow,
            CategoryKind = categoryKind,
            ProductCategoryId = categoryKind == FavoriteCategoryKind.Product ? productCategoryId : null
        };

        _db.FavoriteProducts.Add(product);
        await _db.SaveChangesAsync(ct);
        Invalidate(userId);
        _logger.LogInformation("Добавил в избранное продукт {ProductId} пользователя {UserId}", product.Id, userId);

        return (true, product);
    }

    public async Task<FavoriteProduct> SetFixedServingAsync(long userId, int favoriteId, bool isFixedServing, CancellationToken ct)
    {
        var product = await _db.FavoriteProducts
                          .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == favoriteId, ct)
                      ?? throw new InvalidOperationException($"Продукт {favoriteId} пользователя {userId} не найден.");

        product.IsFixedServing = isFixedServing;
        await _db.SaveChangesAsync(ct);
        Invalidate(userId);

        return product;
    }

    public async Task<FavoriteProduct?> DeleteAsync(long userId, int favoriteId, CancellationToken ct)
    {
        var product = await _db.FavoriteProducts
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == favoriteId, ct);

        if (product is null)
        {
            return null;
        }

        _db.FavoriteProducts.Remove(product);
        await _db.SaveChangesAsync(ct);
        Invalidate(userId);
        _logger.LogInformation("Удалил из избранного продукт {ProductId} пользователя {UserId}", favoriteId, userId);

        return product;
    }

    /// <summary>Четыре базовые подкатегории «Продуктов» — заводятся автоматически при первом обращении.</summary>
    private static readonly string[] BuiltInProductCategories =
    {
        "Белковые продукты",
        "Зерновые и крахмалистые",
        "Овощи и фрукты",
        "Жиры"
    };

    public async Task<IReadOnlyList<FavoriteProduct>> GetByCategoryAsync(
        long userId, FavoriteCategoryKind kind, int? productCategoryId, CancellationToken ct)
    {
        var all = await GetAllAsync(userId, ct);
        return all
            .Where(p => p.CategoryKind == kind
                        && (kind != FavoriteCategoryKind.Product || p.ProductCategoryId == productCategoryId))
            .ToList();
    }

    public async Task<IReadOnlyList<ProductCategory>> GetProductCategoriesAsync(long userId, CancellationToken ct)
    {
        var existing = await _db.ProductCategories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            return existing;
        }

        // Первое обращение пользователя к разделу — завожу базовый набор подкатегорий.
        var seeded = BuiltInProductCategories
            .Select(name => new ProductCategory { UserId = userId, Name = name, IsBuiltIn = true })
            .ToList();

        _db.ProductCategories.AddRange(seeded);
        await _db.SaveChangesAsync(ct);

        return seeded.OrderBy(c => c.Name).ToList();
    }

    public async Task<ProductCategory> CreateProductCategoryAsync(long userId, string name, CancellationToken ct)
    {
        var category = new ProductCategory { UserId = userId, Name = name, IsBuiltIn = false };

        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Пользователь {UserId} создал подкатегорию продуктов «{Name}»", userId, name);
        return category;
    }

    public async Task<ProductCategory?> RenameProductCategoryAsync(long userId, int categoryId, string name, CancellationToken ct)
    {
        var category = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);
        if (category is null)
        {
            return null;
        }

        category.Name = name;
        await _db.SaveChangesAsync(ct);
        return category;
    }

    public async Task<bool> DeleteProductCategoryAsync(long userId, int categoryId, CancellationToken ct)
    {
        var category = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);
        if (category is null)
        {
            return false;
        }

        _db.ProductCategories.Remove(category);
        // FK на FavoriteProducts.ProductCategoryId настроен на SetNull — продукты не удаляются, просто теряют подкатегорию.
        await _db.SaveChangesAsync(ct);
        Invalidate(userId);

        _logger.LogInformation("Пользователь {UserId} удалил подкатегорию продуктов {CategoryId}", userId, categoryId);
        return true;
    }

    public async Task<FavoriteProduct?> SetProductCategoryAsync(long userId, int favoriteId, int? productCategoryId, CancellationToken ct)
    {
        var product = await _db.FavoriteProducts.FirstOrDefaultAsync(
            p => p.Id == favoriteId && p.UserId == userId && p.CategoryKind == FavoriteCategoryKind.Product, ct);
        if (product is null)
        {
            return null;
        }

        product.ProductCategoryId = productCategoryId;
        await _db.SaveChangesAsync(ct);
        Invalidate(userId);

        return product;
    }

    public async Task EnsureWaterSeedAsync(long userId, CancellationToken ct)
    {
        var hasWater = await _db.FavoriteProducts.AnyAsync(p => p.UserId == userId && p.CategoryKind == FavoriteCategoryKind.Water, ct);
        if (hasWater)
        {
            return;
        }

        _db.FavoriteProducts.Add(new FavoriteProduct
        {
            UserId = userId,
            Name = "Вода",
            Calories = 0,
            Proteins = 0m,
            Fats = 0m,
            Carbs = 0m,
            IsFixedServing = false,
            CategoryKind = FavoriteCategoryKind.Water,
            CreatedAt = _clock.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        Invalidate(userId);
    }

    public async Task<FavoriteProduct> CreateDishAsync(long userId, string name, CancellationToken ct)
    {
        var dish = new FavoriteProduct
        {
            UserId = userId,
            Name = name,
            Calories = 0,
            Proteins = 0m,
            Fats = 0m,
            Carbs = 0m,
            IsFixedServing = true,
            CategoryKind = FavoriteCategoryKind.Dish,
            CreatedAt = _clock.UtcNow
        };

        _db.FavoriteProducts.Add(dish);
        await _db.SaveChangesAsync(ct);
        Invalidate(userId);

        _logger.LogInformation("Пользователь {UserId} создал блюдо {DishId} «{Name}»", userId, dish.Id, name);
        return dish;
    }

    public async Task<IReadOnlyList<DishIngredient>> GetDishIngredientsAsync(long userId, int dishId, CancellationToken ct) =>
        await _db.DishIngredients
            .AsNoTracking()
            .Where(i => i.DishFavoriteProductId == dishId && i.DishFavoriteProduct!.UserId == userId)
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

    public async Task<FavoriteProduct?> AddDishIngredientAsync(long userId, int dishId, ProductDraft ingredient, CancellationToken ct)
    {
        var dish = await _db.FavoriteProducts.FirstOrDefaultAsync(
            p => p.Id == dishId && p.UserId == userId && p.CategoryKind == FavoriteCategoryKind.Dish, ct);
        if (dish is null)
        {
            return null;
        }

        _db.DishIngredients.Add(new DishIngredient
        {
            DishFavoriteProductId = dishId,
            Name = ingredient.Name,
            Proteins = ingredient.Proteins,
            Fats = ingredient.Fats,
            Carbs = ingredient.Carbs,
            Calories = ingredient.Calories
        });

        await RecomputeDishAsync(dish, ct);
        return dish;
    }

    public async Task<FavoriteProduct?> RemoveDishIngredientAsync(long userId, int dishId, int ingredientId, CancellationToken ct)
    {
        var dish = await _db.FavoriteProducts.FirstOrDefaultAsync(
            p => p.Id == dishId && p.UserId == userId && p.CategoryKind == FavoriteCategoryKind.Dish, ct);
        if (dish is null)
        {
            return null;
        }

        var ingredient = await _db.DishIngredients.FirstOrDefaultAsync(i => i.Id == ingredientId && i.DishFavoriteProductId == dishId, ct);
        if (ingredient is null)
        {
            return dish;
        }

        _db.DishIngredients.Remove(ingredient);
        await RecomputeDishAsync(dish, ct);
        return dish;
    }

    /// <summary>Пересчитываю сохранённое КБЖУ блюда автосуммой его текущих ингредиентов.</summary>
    private async Task RecomputeDishAsync(FavoriteProduct dish, CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);

        var totals = await _db.DishIngredients
            .Where(i => i.DishFavoriteProductId == dish.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Proteins = g.Sum(i => i.Proteins),
                Fats = g.Sum(i => i.Fats),
                Carbs = g.Sum(i => i.Carbs),
                Calories = g.Sum(i => i.Calories)
            })
            .FirstOrDefaultAsync(ct);

        dish.Proteins = totals?.Proteins ?? 0m;
        dish.Fats = totals?.Fats ?? 0m;
        dish.Carbs = totals?.Carbs ?? 0m;
        dish.Calories = totals?.Calories ?? 0;

        await _db.SaveChangesAsync(ct);
        Invalidate(dish.UserId);
    }

    /// <summary>Сбрасываю кэш списка после любой записи — иначе пользователь увидит старые данные.</summary>
    private void Invalidate(long userId) => _cache.Remove(CacheKey(userId));

    private static string CacheKey(long userId) => $"favorites:{userId}";
}

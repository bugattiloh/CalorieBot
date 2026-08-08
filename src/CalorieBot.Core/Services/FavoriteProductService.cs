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
    /// Добавляю продукт в избранное. Если продукт с таким названием уже есть, обновляю его КБЖУ
    /// и возвращаю Created = false — так пользователь не получит ошибку из-за дубля.
    /// </summary>
    Task<(bool Created, FavoriteProduct Product)> AddOrUpdateAsync(long userId, ProductDraft draft, CancellationToken ct);

    /// <summary>Удаляю продукт из избранного. Записи журнала при этом остаются.</summary>
    Task<FavoriteProduct?> DeleteAsync(long userId, int favoriteId, CancellationToken ct);
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
        CancellationToken ct)
    {
        var existing = await _db.FavoriteProducts
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Name == draft.Name, ct);

        if (existing is not null)
        {
            existing.Calories = draft.Calories;
            existing.Proteins = draft.Proteins;
            existing.Fats = draft.Fats;
            existing.Carbs = draft.Carbs;
            existing.ServingSize = draft.ServingSize ?? existing.ServingSize;

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
            ServingSize = draft.ServingSize,
            CreatedAt = _clock.UtcNow
        };

        _db.FavoriteProducts.Add(product);
        await _db.SaveChangesAsync(ct);
        Invalidate(userId);
        _logger.LogInformation("Добавил в избранное продукт {ProductId} пользователя {UserId}", product.Id, userId);

        return (true, product);
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

    /// <summary>Сбрасываю кэш списка после любой записи — иначе пользователь увидит старые данные.</summary>
    private void Invalidate(long userId) => _cache.Remove(CacheKey(userId));

    private static string CacheKey(long userId) => $"favorites:{userId}";
}

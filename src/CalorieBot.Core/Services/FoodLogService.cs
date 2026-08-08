using CalorieBot.Core.Models;
using CalorieBot.Core.Time;
using CalorieBot.Data;
using CalorieBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CalorieBot.Core.Services;

/// <summary>Дневник питания.</summary>
public interface IFoodLogService
{
    /// <summary>Записываю съеденный продукт в журнал.</summary>
    Task<FoodLogEntry> LogAsync(
        long userId,
        ProductDraft draft,
        MealType mealType,
        int? favoriteProductId,
        CancellationToken ct);

    /// <summary>Всё, что съедено за текущий «бот-день» (UTC+3), по порядку добавления.</summary>
    Task<IReadOnlyList<FoodLogEntry>> GetTodayAsync(long userId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class FoodLogService : IFoodLogService
{
    private readonly CalorieBotDbContext _db;
    private readonly IDayClock _clock;
    private readonly ILogger<FoodLogService> _logger;

    public FoodLogService(CalorieBotDbContext db, IDayClock clock, ILogger<FoodLogService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FoodLogEntry> LogAsync(
        long userId,
        ProductDraft draft,
        MealType mealType,
        int? favoriteProductId,
        CancellationToken ct)
    {
        var entry = new FoodLogEntry
        {
            UserId = userId,
            ProductName = draft.Name,
            Calories = draft.Calories,
            Proteins = draft.Proteins,
            Fats = draft.Fats,
            Carbs = draft.Carbs,
            ServingSize = draft.ServingSize,
            MealType = mealType,
            LoggedAt = _clock.UtcNow,
            IsFavorite = favoriteProductId.HasValue,
            FavoriteProductId = favoriteProductId
        };

        _db.FoodLog.Add(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Записал приём пищи {EntryId}: {ProductName}, {Calories} ккал, тип {MealType}, пользователь {UserId}",
            entry.Id, entry.ProductName, entry.Calories, entry.MealType, userId);

        return entry;
    }

    public async Task<IReadOnlyList<FoodLogEntry>> GetTodayAsync(long userId, CancellationToken ct)
    {
        // Дневной счётчик обнуляется в полночь по UTC+3 — вместо «чистки» просто беру срез за нужный интервал.
        var (startUtc, endUtc) = _clock.TodayUtcRange;

        return await _db.FoodLog
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.LoggedAt >= startUtc && e.LoggedAt < endUtc)
            .OrderBy(e => e.LoggedAt)
            .ToListAsync(ct);
    }
}

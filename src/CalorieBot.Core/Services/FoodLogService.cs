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

    /// <summary>Всё, что съедено с начала текущего цикла (<see cref="AppUser.CycleStartedAt"/>), по порядку добавления.</summary>
    Task<IReadOnlyList<FoodLogEntry>> GetCurrentCycleAsync(long userId, CancellationToken ct);

    /// <summary>Удаляю запись журнала (например, при замене дубля новой записью). Возвращаю false, если её уже нет.</summary>
    Task<bool> DeleteAsync(long userId, int entryId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class FoodLogService : IFoodLogService
{
    private readonly CalorieBotDbContext _db;
    private readonly IUserService _users;
    private readonly IDayClock _clock;
    private readonly ILogger<FoodLogService> _logger;

    public FoodLogService(CalorieBotDbContext db, IUserService users, IDayClock clock, ILogger<FoodLogService> logger)
    {
        _db = db;
        _users = users;
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

    public async Task<IReadOnlyList<FoodLogEntry>> GetCurrentCycleAsync(long userId, CancellationToken ct)
    {
        // Цикл не «чистится» по расписанию — вместо этого беру срез с момента его начала.
        var user = await _users.GetAsync(userId, ct);

        return await _db.FoodLog
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.LoggedAt >= user.CycleStartedAt)
            .OrderBy(e => e.LoggedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteAsync(long userId, int entryId, CancellationToken ct)
    {
        var entry = await _db.FoodLog.FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == userId, ct);
        if (entry is null)
        {
            return false;
        }

        _db.FoodLog.Remove(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Удалил запись журнала {EntryId} ({ProductName}) у пользователя {UserId}", entry.Id, entry.ProductName, userId);
        return true;
    }
}

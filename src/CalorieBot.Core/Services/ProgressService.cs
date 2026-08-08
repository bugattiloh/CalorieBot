using CalorieBot.Core.Models;
using CalorieBot.Core.Time;
using CalorieBot.Data;
using CalorieBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CalorieBot.Core.Services;

/// <summary>Прогресс по дневному лимиту.</summary>
public interface IProgressService
{
    /// <summary>Считаю прогресс за текущий день и подбираю избранное, влезающее в остаток.</summary>
    Task<DailyProgress> GetTodayAsync(long userId, CancellationToken ct);

    /// <summary>Только избранное, которое вписывается в остаток лимита.</summary>
    Task<IReadOnlyList<FavoriteProduct>> GetFittingFavoritesAsync(long userId, int remainingCalories, CancellationToken ct);
}

/// <inheritdoc />
public sealed class ProgressService : IProgressService
{
    /// <summary>Сколько подходящих продуктов показываю в прогрессе, чтобы не раздувать сообщение.</summary>
    private const int MaxFittingFavorites = 10;

    private readonly CalorieBotDbContext _db;
    private readonly IUserService _users;
    private readonly IFavoriteProductService _favorites;
    private readonly IDayClock _clock;

    public ProgressService(
        CalorieBotDbContext db,
        IUserService users,
        IFavoriteProductService favorites,
        IDayClock clock)
    {
        _db = db;
        _users = users;
        _favorites = favorites;
        _clock = clock;
    }

    public async Task<DailyProgress> GetTodayAsync(long userId, CancellationToken ct)
    {
        var user = await _users.GetAsync(userId, ct);
        var (startUtc, endUtc) = _clock.TodayUtcRange;

        // Суммы считаю на стороне Postgres — тянуть все записи дня в память незачем.
        var totals = await _db.FoodLog
            .Where(e => e.UserId == userId && e.LoggedAt >= startUtc && e.LoggedAt < endUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Calories = g.Sum(e => e.Calories),
                Proteins = g.Sum(e => e.Proteins),
                Fats = g.Sum(e => e.Fats),
                Carbs = g.Sum(e => e.Carbs),
                Count = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        var consumed = totals?.Calories ?? 0;
        var remaining = Math.Max(0, user.DailyCalorieLimit - consumed);

        return new DailyProgress
        {
            LocalDate = _clock.LocalToday,
            CalorieLimit = user.DailyCalorieLimit,
            ConsumedCalories = consumed,
            Proteins = totals?.Proteins ?? 0m,
            Fats = totals?.Fats ?? 0m,
            Carbs = totals?.Carbs ?? 0m,
            ProteinsLimit = user.DailyProteinsLimit,
            FatsLimit = user.DailyFatsLimit,
            CarbsLimit = user.DailyCarbsLimit,
            EntriesCount = totals?.Count ?? 0,
            TimeUntilReset = _clock.TimeUntilReset,
            FittingFavorites = await GetFittingFavoritesAsync(userId, remaining, ct)
        };
    }

    public async Task<IReadOnlyList<FavoriteProduct>> GetFittingFavoritesAsync(
        long userId,
        int remainingCalories,
        CancellationToken ct)
    {
        if (remainingCalories <= 0)
        {
            return Array.Empty<FavoriteProduct>();
        }

        var favorites = await _favorites.GetAllAsync(userId, ct);

        // Сначала самые калорийные из подходящих: их сложнее «уместить» позже в течение дня.
        return favorites
            .Where(p => p.Calories <= remainingCalories)
            .OrderByDescending(p => p.Calories)
            .ThenBy(p => p.Name)
            .Take(MaxFittingFavorites)
            .ToList();
    }
}

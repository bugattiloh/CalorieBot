using CalorieBot.Core.Models;
using CalorieBot.Core.Time;
using CalorieBot.Data;
using CalorieBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CalorieBot.Core.Services;

/// <summary>Прогресс по дневному лимиту в рамках текущего цикла подсчёта КБЖУ.</summary>
public interface IProgressService
{
    /// <summary>Считаю прогресс за текущий цикл (с <see cref="AppUser.CycleStartedAt"/>) и подбираю избранное, влезающее в остаток.</summary>
    Task<DailyProgress> GetCurrentCycleAsync(long userId, CancellationToken ct);

    /// <summary>Только избранное, которое вписывается в остаток по правилам <paramref name="progress"/> (см. <see cref="DailyProgress.Fits"/>).</summary>
    Task<IReadOnlyList<FavoriteProduct>> GetFittingFavoritesAsync(long userId, DailyProgress progress, CancellationToken ct);
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

    public async Task<DailyProgress> GetCurrentCycleAsync(long userId, CancellationToken ct)
    {
        var user = await _users.GetAsync(userId, ct);
        var cycleStart = user.CycleStartedAt;

        // Суммы считаю на стороне Postgres — тянуть все записи цикла в память незачем.
        var totals = await _db.FoodLog
            .Where(e => e.UserId == userId && e.LoggedAt >= cycleStart)
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

        var progressWithoutFavorites = new DailyProgress
        {
            CycleStartedAt = cycleStart,
            TrackingMode = user.TrackingMode,
            CalorieLimit = user.DailyCalorieLimit,
            ConsumedCalories = totals?.Calories ?? 0,
            Proteins = totals?.Proteins ?? 0m,
            Fats = totals?.Fats ?? 0m,
            Carbs = totals?.Carbs ?? 0m,
            ProteinsLimit = user.DailyProteinsLimit,
            FatsLimit = user.DailyFatsLimit,
            CarbsLimit = user.DailyCarbsLimit,
            EntriesCount = totals?.Count ?? 0,
            CycleElapsed = _clock.UtcNow - cycleStart
        };

        return progressWithoutFavorites with
        {
            FittingFavorites = await GetFittingFavoritesAsync(userId, progressWithoutFavorites, ct)
        };
    }

    public async Task<IReadOnlyList<FavoriteProduct>> GetFittingFavoritesAsync(
        long userId,
        DailyProgress progress,
        CancellationToken ct)
    {
        var favorites = await _favorites.GetAllAsync(userId, ct);
        var fitting = favorites.Where(progress.Fits);

        // В режиме БЖУ сортирую по суммарному весу нутриентов, в режиме калорий — по калориям:
        // в обоих случаях сначала показываю то, что сложнее «уместить» позже в цикле.
        fitting = progress.TrackingMode == CalorieTrackingMode.Macros
            ? fitting.OrderByDescending(p => p.Proteins + p.Fats + p.Carbs).ThenBy(p => p.Name)
            : fitting.OrderByDescending(p => p.Calories).ThenBy(p => p.Name);

        return fitting.Take(MaxFittingFavorites).ToList();
    }
}

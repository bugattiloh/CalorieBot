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

    /// <summary>
    /// Сводка за <paramref name="periodDays"/> последних завершённых календарных дней (сегодняшний
    /// день ещё не закончился, поэтому в отчёт не попадает).
    /// </summary>
    Task<PeriodReport> GetReportAsync(long userId, int periodDays, CancellationToken ct);
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

    public async Task<PeriodReport> GetReportAsync(long userId, int periodDays, CancellationToken ct)
    {
        var user = await _users.GetAsync(userId, ct);

        var todayLocal = DateOnly.FromDateTime(_clock.UtcNow + _clock.Offset);
        var periodStartLocal = todayLocal.AddDays(-periodDays);

        // DateOnly.ToDateTime всегда отдаёт Kind=Unspecified — Npgsql такой DateTime в параметр
        // под timestamptz не примет (кинет исключение), поэтому явно проставляю Utc: тикскоунт от этого
        // не меняется, а значение и так уже пересчитано в UTC вычитанием смещения.
        var periodStartUtc = DateTime.SpecifyKind(periodStartLocal.ToDateTime(TimeOnly.MinValue) - _clock.Offset, DateTimeKind.Utc);
        var periodEndUtc = DateTime.SpecifyKind(todayLocal.ToDateTime(TimeOnly.MinValue) - _clock.Offset, DateTimeKind.Utc);
        // Верхняя граница — начало сегодняшнего дня: сегодняшний, ещё не законченный день в отчёт не включаю.

        var entries = await _db.FoodLog
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.LoggedAt >= periodStartUtc && e.LoggedAt < periodEndUtc)
            .ToListAsync(ct);

        // Группирую по локальному календарному дню на стороне клиента — данных за месяц немного,
        // а трансляция такого группирования в SQL приносит больше риска, чем пользы.
        var byDay = entries
            .GroupBy(e => DateOnly.FromDateTime(e.LoggedAt + _clock.Offset))
            .Select(g => new
            {
                Day = g.Key,
                Calories = g.Sum(e => e.Calories),
                Proteins = g.Sum(e => e.Proteins),
                Fats = g.Sum(e => e.Fats),
                Carbs = g.Sum(e => e.Carbs)
            })
            .OrderBy(d => d.Day)
            .ToList();

        var daysWithData = byDay.Count;
        var last = byDay.Count == 0 ? null : byDay[^1];

        return new PeriodReport
        {
            PeriodDays = periodDays,
            DaysWithData = daysWithData,
            TrackingMode = user.TrackingMode,
            CalorieLimit = user.DailyCalorieLimit,
            AverageCalories = daysWithData == 0 ? 0 : (int)Math.Round(byDay.Average(d => d.Calories), MidpointRounding.AwayFromZero),
            DaysOverCalorieLimit = byDay.Count(d => d.Calories > user.DailyCalorieLimit),
            DaysUnderCalorieLimit = byDay.Count(d => d.Calories < user.DailyCalorieLimit),
            ProteinsLimit = user.DailyProteinsLimit,
            FatsLimit = user.DailyFatsLimit,
            CarbsLimit = user.DailyCarbsLimit,
            AverageProteins = daysWithData == 0 ? 0m : Math.Round(byDay.Average(d => d.Proteins), 1),
            AverageFats = daysWithData == 0 ? 0m : Math.Round(byDay.Average(d => d.Fats), 1),
            AverageCarbs = daysWithData == 0 ? 0m : Math.Round(byDay.Average(d => d.Carbs), 1),
            LastDayWithData = last?.Day,
            LastDayCalories = last?.Calories ?? 0,
            LastDayProteins = last?.Proteins ?? 0m,
            LastDayFats = last?.Fats ?? 0m,
            LastDayCarbs = last?.Carbs ?? 0m
        };
    }
}

using CalorieBot.Core.Time;
using CalorieBot.Data;
using CalorieBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CalorieBot.Core.Services;

/// <summary>
/// Циклы подсчёта КБЖУ. У людей разные жизненные ритмы, поэтому автоматического сброса
/// по календарным суткам нет — пользователь сам решает, когда начать «новый день».
/// </summary>
public interface ICycleService
{
    /// <summary>
    /// Закрываю текущий цикл: сохраняю его как запись истории и сдвигаю начало нового цикла на «сейчас».
    /// Записи FoodLog при этом никуда не деваются — они просто перестают попадать в текущий цикл.
    /// </summary>
    Task<CalorieCycle> StartNewCycleAsync(long userId, CancellationToken ct);

    /// <summary>Прошлые циклы, новые сверху.</summary>
    Task<IReadOnlyList<CalorieCycle>> GetHistoryAsync(long userId, int skip, int take, CancellationToken ct);

    /// <summary>Сколько всего прошлых циклов у пользователя — нужно для пагинации.</summary>
    Task<int> GetHistoryCountAsync(long userId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class CycleService : ICycleService
{
    private readonly CalorieBotDbContext _db;
    private readonly IUserService _users;
    private readonly IDayClock _clock;
    private readonly ILogger<CycleService> _logger;

    public CycleService(CalorieBotDbContext db, IUserService users, IDayClock clock, ILogger<CycleService> logger)
    {
        _db = db;
        _users = users;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CalorieCycle> StartNewCycleAsync(long userId, CancellationToken ct)
    {
        var user = await _users.GetAsync(userId, ct);
        var now = _clock.UtcNow;
        var cycleStart = user.CycleStartedAt;

        // Снимок закрываемого цикла считаю на стороне Postgres — так же, как текущий прогресс.
        var totals = await _db.FoodLog
            .Where(e => e.UserId == userId && e.LoggedAt >= cycleStart && e.LoggedAt <= now)
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

        var closedCycle = new CalorieCycle
        {
            UserId = userId,
            StartedAt = cycleStart,
            EndedAt = now,
            CalorieLimit = user.DailyCalorieLimit,
            ConsumedCalories = totals?.Calories ?? 0,
            Proteins = totals?.Proteins ?? 0m,
            Fats = totals?.Fats ?? 0m,
            Carbs = totals?.Carbs ?? 0m,
            EntriesCount = totals?.Count ?? 0
        };

        _db.CalorieCycles.Add(closedCycle);
        await _db.SaveChangesAsync(ct);

        // Отдельным шагом — через UserService, чтобы его кэш профиля тоже обновился.
        await _users.SetCycleStartAsync(userId, now, ct);

        _logger.LogInformation(
            "Пользователь {UserId} закрыл цикл {Start:O}–{End:O}: {Calories} из {Limit} ккал, начал новый",
            userId, closedCycle.StartedAt, closedCycle.EndedAt, closedCycle.ConsumedCalories, closedCycle.CalorieLimit);

        return closedCycle;
    }

    public async Task<IReadOnlyList<CalorieCycle>> GetHistoryAsync(long userId, int skip, int take, CancellationToken ct) =>
        await _db.CalorieCycles
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.EndedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> GetHistoryCountAsync(long userId, CancellationToken ct) =>
        _db.CalorieCycles.CountAsync(c => c.UserId == userId, ct);
}

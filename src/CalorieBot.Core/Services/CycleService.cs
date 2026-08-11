using CalorieBot.Core.Models;
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

    /// <summary>Один прошлый цикл по Id, если он принадлежит пользователю.</summary>
    Task<CalorieCycle?> GetAsync(long userId, int cycleId, CancellationToken ct);

    /// <summary>Записи журнала, попавшие в границы конкретного (в том числе уже закрытого) цикла.</summary>
    Task<IReadOnlyList<FoodLogEntry>> GetEntriesAsync(long userId, int cycleId, CancellationToken ct);

    /// <summary>
    /// Удаляю запись из закрытого цикла и пересчитываю его сохранённый снимок (калории/БЖУ/счётчик).
    /// Возвращаю false, если цикла или записи в его границах уже нет.
    /// </summary>
    Task<bool> DeleteEntryAsync(long userId, int cycleId, int entryId, CancellationToken ct);

    /// <summary>
    /// Добавляю запись задним числом в уже закрытый цикл и пересчитываю его снимок.
    /// Время записи ставлю на последний момент цикла (минус тик), чтобы она не задвоилась в соседнем цикле.
    /// </summary>
    Task<FoodLogEntry> AddEntryAsync(long userId, int cycleId, ProductDraft draft, MealType mealType, CancellationToken ct);
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

    public Task<CalorieCycle?> GetAsync(long userId, int cycleId, CancellationToken ct) =>
        _db.CalorieCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId && c.UserId == userId, ct);

    public async Task<IReadOnlyList<FoodLogEntry>> GetEntriesAsync(long userId, int cycleId, CancellationToken ct)
    {
        var cycle = await GetAsync(userId, cycleId, ct);
        if (cycle is null)
        {
            return Array.Empty<FoodLogEntry>();
        }

        return await _db.FoodLog
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.LoggedAt >= cycle.StartedAt && e.LoggedAt <= cycle.EndedAt)
            .OrderBy(e => e.LoggedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteEntryAsync(long userId, int cycleId, int entryId, CancellationToken ct)
    {
        var cycle = await _db.CalorieCycles.FirstOrDefaultAsync(c => c.Id == cycleId && c.UserId == userId, ct);
        if (cycle is null)
        {
            return false;
        }

        var entry = await _db.FoodLog.FirstOrDefaultAsync(
            e => e.Id == entryId && e.UserId == userId && e.LoggedAt >= cycle.StartedAt && e.LoggedAt <= cycle.EndedAt, ct);
        if (entry is null)
        {
            return false;
        }

        _db.FoodLog.Remove(entry);
        await RecomputeSnapshotAsync(cycle, ct);

        _logger.LogInformation(
            "Пользователь {UserId} удалил запись {EntryId} из прошлого цикла {CycleId}", userId, entryId, cycleId);
        return true;
    }

    public async Task<FoodLogEntry> AddEntryAsync(long userId, int cycleId, ProductDraft draft, MealType mealType, CancellationToken ct)
    {
        var cycle = await _db.CalorieCycles.FirstAsync(c => c.Id == cycleId && c.UserId == userId, ct);

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
            // Минус тик от конца цикла: строго внутри его границ, но не на стыке со следующим циклом
            // (у него StartedAt равен EndedAt этого — совпадение по «<=» задвоило бы запись в обоих).
            LoggedAt = cycle.EndedAt.AddTicks(-1),
            IsFavorite = false,
            FavoriteProductId = null
        };

        _db.FoodLog.Add(entry);
        await RecomputeSnapshotAsync(cycle, ct);

        _logger.LogInformation(
            "Пользователь {UserId} добавил запись «{ProductName}» в прошлый цикл {CycleId}", userId, entry.ProductName, cycleId);
        return entry;
    }

    /// <summary>Пересчитываю сохранённый снимок цикла по фактическим записям в его границах.</summary>
    private async Task RecomputeSnapshotAsync(CalorieCycle cycle, CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);

        var totals = await _db.FoodLog
            .Where(e => e.UserId == cycle.UserId && e.LoggedAt >= cycle.StartedAt && e.LoggedAt <= cycle.EndedAt)
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

        cycle.ConsumedCalories = totals?.Calories ?? 0;
        cycle.Proteins = totals?.Proteins ?? 0m;
        cycle.Fats = totals?.Fats ?? 0m;
        cycle.Carbs = totals?.Carbs ?? 0m;
        cycle.EntriesCount = totals?.Count ?? 0;

        await _db.SaveChangesAsync(ct);
    }
}

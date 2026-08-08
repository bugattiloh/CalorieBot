using CalorieBot.Core.Nutrition;
using CalorieBot.Core.Time;
using CalorieBot.Data;
using CalorieBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CalorieBot.Core.Services;

/// <summary>Работа с профилем пользователя и его дневным лимитом.</summary>
public interface IUserService
{
    /// <summary>Нахожу пользователя или создаю нового с дефолтным лимитом 2000 ккал.</summary>
    Task<AppUser> GetOrCreateAsync(long userId, string? username, string? firstName, CancellationToken ct);

    /// <summary>Отдаю профиль (по возможности из кэша).</summary>
    Task<AppUser> GetAsync(long userId, CancellationToken ct);

    /// <summary>Меняю дневной максимум калорий и пересчитываю ориентиры по БЖУ.</summary>
    Task<AppUser> UpdateCalorieLimitAsync(long userId, int newLimit, CancellationToken ct);
}

/// <inheritdoc />
public sealed class UserService : IUserService
{
    /// <summary>Лимит по умолчанию при первом запуске — как в требованиях.</summary>
    public const int DefaultCalorieLimit = 2000;

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly CalorieBotDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IDayClock _clock;
    private readonly ILogger<UserService> _logger;

    public UserService(
        CalorieBotDbContext db,
        IMemoryCache cache,
        IDayClock clock,
        ILogger<UserService> logger)
    {
        _db = db;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AppUser> GetOrCreateAsync(long userId, string? username, string? firstName, CancellationToken ct)
    {
        // Этот метод зовётся на каждый апдейт, поэтому при совпадении с кэшем в базу не хожу вовсе.
        if (_cache.TryGetValue(CacheKey(userId), out AppUser? cached)
            && cached is not null
            && cached.Username == username
            && cached.FirstName == firstName)
        {
            return cached;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user is null)
        {
            var macros = CalorieCalculator.SplitLimitToMacros(DefaultCalorieLimit);
            user = new AppUser
            {
                UserId = userId,
                Username = username,
                FirstName = firstName,
                DailyCalorieLimit = DefaultCalorieLimit,
                DailyProteinsLimit = macros.Proteins,
                DailyFatsLimit = macros.Fats,
                DailyCarbsLimit = macros.Carbs,
                CreatedAt = _clock.UtcNow,
                GoalSetAt = null
            };

            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Создал профиль пользователя {UserId} с лимитом {Limit} ккал", userId, DefaultCalorieLimit);
            }
            catch (DbUpdateException ex)
            {
                // Два быстрых апдейта подряд могут попробовать создать профиль одновременно —
                // в этом случае просто перечитываю уже существующую запись.
                _logger.LogWarning(ex, "Профиль {UserId} уже создан параллельно, перечитываю", userId);
                _db.Entry(user).State = EntityState.Detached;
                user = await _db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId, ct);
            }
        }
        else if (user.Username != username || user.FirstName != firstName)
        {
            // Имя и username в Telegram меняются — подтягиваю актуальные значения.
            user.Username = username;
            user.FirstName = firstName;
            await _db.SaveChangesAsync(ct);
        }

        return Cache(user);
    }

    public async Task<AppUser> GetAsync(long userId, CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey(userId), out AppUser? cached) && cached is not null)
        {
            return cached;
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            // Сюда попадаю, только если базу почистили под работающим ботом — восстанавливаю профиль.
            _logger.LogWarning("Профиль {UserId} не найден, создаю заново", userId);
            return await GetOrCreateAsync(userId, username: null, firstName: null, ct);
        }

        return Cache(user);
    }

    public async Task<AppUser> UpdateCalorieLimitAsync(long userId, int newLimit, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new InvalidOperationException($"Пользователь {userId} не найден.");

        var macros = CalorieCalculator.SplitLimitToMacros(newLimit);

        user.DailyCalorieLimit = newLimit;
        user.DailyProteinsLimit = macros.Proteins;
        user.DailyFatsLimit = macros.Fats;
        user.DailyCarbsLimit = macros.Carbs;
        // Лимит живёт до следующей явной замены, поэтому фиксирую момент установки.
        user.GoalSetAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Пользователь {UserId} сменил дневной лимит на {Limit} ккал", userId, newLimit);

        return Cache(user);
    }

    /// <summary>
    /// Кладу в кэш отсоединённую копию: сама сущность привязана к scoped-контексту,
    /// и делить её между запросами нельзя.
    /// </summary>
    private AppUser Cache(AppUser user)
    {
        var snapshot = new AppUser
        {
            UserId = user.UserId,
            Username = user.Username,
            FirstName = user.FirstName,
            DailyCalorieLimit = user.DailyCalorieLimit,
            DailyProteinsLimit = user.DailyProteinsLimit,
            DailyFatsLimit = user.DailyFatsLimit,
            DailyCarbsLimit = user.DailyCarbsLimit,
            CreatedAt = user.CreatedAt,
            GoalSetAt = user.GoalSetAt
        };

        _cache.Set(CacheKey(user.UserId), snapshot, CacheLifetime);
        return snapshot;
    }

    private static string CacheKey(long userId) => $"user:{userId}";
}

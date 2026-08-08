using CalorieBot.Data.Entities;

namespace CalorieBot.Core.Models;

/// <summary>
/// Срез дня: сколько съедено, сколько осталось до лимита и что из избранного ещё вписывается.
/// Все производные величины считаю здесь, чтобы хендлеры занимались только текстом.
/// </summary>
public sealed record DailyProgress
{
    /// <summary>Дата «бот-дня» (UTC+3), за который посчитан прогресс.</summary>
    public required DateOnly LocalDate { get; init; }

    public required int CalorieLimit { get; init; }

    public required int ConsumedCalories { get; init; }

    public decimal Proteins { get; init; }

    public decimal Fats { get; init; }

    public decimal Carbs { get; init; }

    public decimal? ProteinsLimit { get; init; }

    public decimal? FatsLimit { get; init; }

    public decimal? CarbsLimit { get; init; }

    public int EntriesCount { get; init; }

    /// <summary>Сколько осталось до полуночи по UTC+3 — показываю в прогрессе.</summary>
    public TimeSpan TimeUntilReset { get; init; }

    /// <summary>Избранное, которое ещё влезает в остаток лимита.</summary>
    public IReadOnlyList<FavoriteProduct> FittingFavorites { get; init; } = Array.Empty<FavoriteProduct>();

    /// <summary>Остаток лимита. Ниже нуля не опускаю — для перебора есть <see cref="ExceededBy"/>.</summary>
    public int RemainingCalories => Math.Max(0, CalorieLimit - ConsumedCalories);

    public bool IsExceeded => ConsumedCalories > CalorieLimit;

    public int ExceededBy => Math.Max(0, ConsumedCalories - CalorieLimit);

    /// <summary>Процент от лимита. Может быть больше 100 — так и задумано.</summary>
    public int PercentUsed => CalorieLimit <= 0
        ? 0
        : (int)Math.Round(ConsumedCalories * 100m / CalorieLimit, MidpointRounding.AwayFromZero);
}

using CalorieBot.Data.Entities;

namespace CalorieBot.Core.Models;

/// <summary>
/// Срез текущего цикла подсчёта КБЖУ: сколько съедено, сколько осталось до лимита и что из избранного
/// ещё вписывается. Все производные величины считаю здесь, чтобы хендлеры занимались только текстом.
/// </summary>
public sealed record DailyProgress
{
    /// <summary>Когда пользователь начал текущий цикл (UTC) — вручную кнопкой «Новый день» или при регистрации.</summary>
    public required DateTime CycleStartedAt { get; init; }

    public required int CalorieLimit { get; init; }

    public required int ConsumedCalories { get; init; }

    public decimal Proteins { get; init; }

    public decimal Fats { get; init; }

    public decimal Carbs { get; init; }

    public decimal? ProteinsLimit { get; init; }

    public decimal? FatsLimit { get; init; }

    public decimal? CarbsLimit { get; init; }

    public int EntriesCount { get; init; }

    /// <summary>Сколько времени идёт текущий цикл — показываю в прогрессе вместо «сброса по расписанию».</summary>
    public TimeSpan CycleElapsed { get; init; }

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

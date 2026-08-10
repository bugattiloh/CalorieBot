using CalorieBot.Data.Entities;

namespace CalorieBot.Core.Models;

/// <summary>
/// Срез текущего цикла подсчёта КБЖУ: сколько съедено, сколько осталось до лимита и что из избранного
/// ещё вписывается. Все производные величины считаю здесь, чтобы хендлеры занимались только текстом.
/// </summary>
public sealed record DailyProgress
{
    /// <summary>Позволяю сравнению «влезает ли продукт в остаток» немного превышать точный остаток — иначе законное округление БЖУ то и дело портит подбор.</summary>
    private const decimal MacroFitToleranceGrams = 5m;

    /// <summary>Когда пользователь начал текущий цикл (UTC) — вручную кнопкой «Новый день» или при регистрации.</summary>
    public required DateTime CycleStartedAt { get; init; }

    /// <summary>Что пользователь отслеживает в этом цикле — от этого зависит, что показывать как «остаток».</summary>
    public required CalorieTrackingMode TrackingMode { get; init; }

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

    public decimal ProteinsRemaining => ProteinsLimit is null ? 0m : Math.Max(0m, ProteinsLimit.Value - Proteins);

    public bool IsProteinsExceeded => ProteinsLimit is not null && Proteins > ProteinsLimit.Value;

    public decimal ProteinsExceededBy => ProteinsLimit is null ? 0m : Math.Max(0m, Proteins - ProteinsLimit.Value);

    public decimal FatsRemaining => FatsLimit is null ? 0m : Math.Max(0m, FatsLimit.Value - Fats);

    public bool IsFatsExceeded => FatsLimit is not null && Fats > FatsLimit.Value;

    public decimal FatsExceededBy => FatsLimit is null ? 0m : Math.Max(0m, Fats - FatsLimit.Value);

    public decimal CarbsRemaining => CarbsLimit is null ? 0m : Math.Max(0m, CarbsLimit.Value - Carbs);

    public bool IsCarbsExceeded => CarbsLimit is not null && Carbs > CarbsLimit.Value;

    public decimal CarbsExceededBy => CarbsLimit is null ? 0m : Math.Max(0m, Carbs - CarbsLimit.Value);

    /// <summary>
    /// Влезает ли продукт в то, что осталось. В режиме <see cref="CalorieTrackingMode.Calories"/> сравниваю
    /// калорийность, в <see cref="CalorieTrackingMode.Macros"/> — сразу все три нутриента (с небольшим допуском).
    /// Для продукта с плавающей порцией (<see cref="FavoriteProduct.IsFixedServing"/> = false) вес заранее
    /// неизвестен, поэтому сравнивать не с чем — считаю, что подходит всегда.
    /// </summary>
    public bool Fits(FavoriteProduct product)
    {
        if (!product.IsFixedServing)
        {
            return true;
        }

        return TrackingMode == CalorieTrackingMode.Macros
            ? product.Proteins <= ProteinsRemaining + MacroFitToleranceGrams
              && product.Fats <= FatsRemaining + MacroFitToleranceGrams
              && product.Carbs <= CarbsRemaining + MacroFitToleranceGrams
            : product.Calories <= RemainingCalories;
    }
}

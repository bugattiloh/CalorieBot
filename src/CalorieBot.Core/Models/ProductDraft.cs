using CalorieBot.Core.Nutrition;
using CalorieBot.Data.Entities;

namespace CalorieBot.Core.Models;

/// <summary>
/// Черновик продукта: то, что пользователь набрал в диалоге, но ещё не сохранено в базу.
/// Использую и при записи в журнал, и при добавлении в избранное.
/// </summary>
public sealed record ProductDraft
{
    public required string Name { get; init; }

    public decimal Proteins { get; init; }

    public decimal Fats { get; init; }

    public decimal Carbs { get; init; }

    public string? ServingSize { get; init; }

    /// <summary>Калорийность. Отдельным полем, чтобы значение из избранного не пересчитывалось заново.</summary>
    public int Calories { get; init; }

    /// <summary>Собираю черновик из введённых БЖУ — калории считаю сам.</summary>
    public static ProductDraft FromMacros(string name, decimal proteins, decimal fats, decimal carbs, string? servingSize = null) =>
        new()
        {
            Name = name,
            Proteins = proteins,
            Fats = fats,
            Carbs = carbs,
            ServingSize = servingSize,
            Calories = CalorieCalculator.FromMacros(proteins, fats, carbs)
        };

    /// <summary>Собираю черновик из уже сохранённого любимого продукта.</summary>
    public static ProductDraft FromFavorite(FavoriteProduct favorite) =>
        new()
        {
            Name = favorite.Name,
            Proteins = favorite.Proteins,
            Fats = favorite.Fats,
            Carbs = favorite.Carbs,
            ServingSize = favorite.ServingSize,
            Calories = favorite.Calories
        };
}

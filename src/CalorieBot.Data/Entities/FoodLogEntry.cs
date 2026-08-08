namespace CalorieBot.Data.Entities;

/// <summary>
/// Запись в дневном журнале питания (таблица FoodLog).
/// КБЖУ дублирую в запись намеренно: если пользователь потом отредактирует или удалит
/// любимый продукт, история за прошлые дни не должна «поехать».
/// </summary>
public class FoodLogEntry
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Calories { get; set; }

    public decimal Proteins { get; set; }

    public decimal Fats { get; set; }

    public decimal Carbs { get; set; }

    public string? ServingSize { get; set; }

    public MealType MealType { get; set; }

    /// <summary>Момент добавления в UTC. Границы «дня» считаю уже в сервисах по UTC+3.</summary>
    public DateTime LoggedAt { get; set; }

    /// <summary>Флаг «продукт был взят из избранного» — нужен для истории и статистики.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Ссылка на избранное, если продукт брался оттуда. При удалении избранного обнуляется.</summary>
    public int? FavoriteProductId { get; set; }

    public AppUser? User { get; set; }

    public FavoriteProduct? FavoriteProduct { get; set; }
}

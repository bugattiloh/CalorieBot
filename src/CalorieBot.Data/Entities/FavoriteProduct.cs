namespace CalorieBot.Data.Entities;

/// <summary>
/// Любимый продукт пользователя (таблица FavoriteProducts).
/// Калорийность храню посчитанной, чтобы не пересчитывать её на каждый запрос списка.
/// </summary>
public class FavoriteProduct
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Калории на порцию. Считаю их из БЖУ формулой Б×4 + Ж×9 + У×4.</summary>
    public int Calories { get; set; }

    public decimal Proteins { get; set; }

    public decimal Fats { get; set; }

    public decimal Carbs { get; set; }

    /// <summary>Описание порции в свободной форме («200 г», «1 стакан»). Необязательное поле.</summary>
    public string? ServingSize { get; set; }

    public DateTime CreatedAt { get; set; }

    public AppUser? User { get; set; }
}

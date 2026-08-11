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

    /// <summary>
    /// true — фиксированная порция (например, батончик): КБЖУ записаны на одну порцию целиком,
    /// продукт логируется в дневник одним тапом как есть.
    /// false — порция каждый раз разная (например, рис): КБЖУ здесь хранятся <b>на 100 г</b>,
    /// а при добавлении в дневник бот спрашивает съеденный вес и пересчитывает КБЖУ на него.
    /// </summary>
    public bool IsFixedServing { get; set; } = true;

    /// <summary>Описание порции в свободной форме («200 г», «1 стакан»). Необязательное поле, актуально только для фиксированной порции.</summary>
    public string? ServingSize { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Вода, готовое блюдо или обычный продукт — определяет, как продукт логируется и где лежит в «Избранном».</summary>
    public FavoriteCategoryKind CategoryKind { get; set; } = FavoriteCategoryKind.Product;

    /// <summary>Подкатегория внутри «Продукты» — только для <see cref="FavoriteCategoryKind.Product"/>, для остальных всегда null.</summary>
    public int? ProductCategoryId { get; set; }

    public AppUser? User { get; set; }

    public ProductCategory? ProductCategory { get; set; }

    /// <summary>Ингредиенты — только для <see cref="FavoriteCategoryKind.Dish"/>, для остальных всегда пусто.</summary>
    public ICollection<DishIngredient> DishIngredients { get; set; } = new List<DishIngredient>();
}

namespace CalorieBot.Data.Entities;

/// <summary>
/// Один ингредиент готового блюда (таблица DishIngredients). КБЖУ храню отдельным снимком —
/// даже если ингредиент был взят из избранного, а потом там изменился или удалился,
/// уже собранное блюдо не должно «поехать».
/// </summary>
public class DishIngredient
{
    public int Id { get; set; }

    /// <summary>Блюдо, к которому относится ингредиент — это <see cref="FavoriteProduct"/> с <see cref="FavoriteCategoryKind.Dish"/>.</summary>
    public int DishFavoriteProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Proteins { get; set; }

    public decimal Fats { get; set; }

    public decimal Carbs { get; set; }

    public int Calories { get; set; }

    public FavoriteProduct? DishFavoriteProduct { get; set; }
}

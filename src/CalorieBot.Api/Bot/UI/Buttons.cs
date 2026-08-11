namespace CalorieBot.Api.Bot.UI;

/// <summary>
/// Подписи кнопок обычной (reply) клавиатуры. Держу их константами:
/// по этим же строкам я потом сравниваю входящие сообщения, поэтому дублировать текст нельзя.
/// </summary>
public static class Buttons
{
    // Главное меню.
    public const string AddMeal = "🍽 Добавить прием пищи";
    public const string Progress = "📊 Мой прогресс";
    public const string Favorites = "⭐ Избранное";
    public const string Limit = "🎯 Дневной лимит";
    public const string NewDay = "🆕 Новый день";
    public const string CycleHistory = "📅 История";

    // Подменю «Добавить прием пищи».
    public const string FromFavorites = "💝 Из любимых";
    public const string NewProduct = "🔍 Новый продукт";

    // Подменю «Избранное» — три группы вместо одного огромного списка.
    public const string Water = "💧 Вода";
    public const string Dishes = "🍲 Готовые блюда";
    public const string Products = "🥘 Продукты";
    public const string DeleteFavorite = "🗑 Удалить из избранного";

    // Подменю «Дневной лимит».
    public const string ChangeLimit = "✏️ Изменить лимит";
    public const string CurrentLimit = "📊 Текущий лимит";

    // Общая кнопка возврата из любого подменю.
    public const string Back = "🔙 Назад";
}

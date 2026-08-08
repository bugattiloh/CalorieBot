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
    public const string Favorites = "⭐ Любимые продукты";
    public const string Limit = "🎯 Дневной лимит";
    public const string History = "📋 История сегодня";

    // Подменю «Добавить прием пищи».
    public const string FromFavorites = "💝 Из любимых";
    public const string NewProduct = "🔍 Новый продукт";

    // Подменю «Любимые продукты».
    public const string AddFavorite = "➕ Добавить в избранное";
    public const string MyProducts = "📝 Мои продукты";
    public const string DeleteFavorite = "🗑 Удалить из избранного";

    // Подменю «Дневной лимит».
    public const string ChangeLimit = "✏️ Изменить лимит калорий";
    public const string CurrentLimit = "📊 Текущий лимит";

    // Общая кнопка возврата из любого подменю.
    public const string Back = "🔙 Назад";
}

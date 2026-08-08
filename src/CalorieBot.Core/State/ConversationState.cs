namespace CalorieBot.Core.State;

/// <summary>
/// Шаг пошагового диалога. Бот кнопочный, но названия и цифры пользователь всё же вводит текстом,
/// и по состоянию я понимаю, чего именно я от него сейчас жду.
/// </summary>
public enum ConversationState
{
    /// <summary>Ничего не жду — обрабатываю только нажатия кнопок.</summary>
    Idle = 0,

    /// <summary>Жду название нового продукта для записи в дневник.</summary>
    AwaitingMealProductName = 1,

    /// <summary>Жду БЖУ нового продукта для записи в дневник.</summary>
    AwaitingMealProductMacros = 2,

    /// <summary>Жду название продукта, который добавляем в избранное.</summary>
    AwaitingFavoriteName = 10,

    /// <summary>Жду БЖУ продукта для избранного.</summary>
    AwaitingFavoriteMacros = 11,

    /// <summary>Жду описание порции для избранного (шаг можно пропустить кнопкой).</summary>
    AwaitingFavoriteServingSize = 12,

    /// <summary>Жду новый дневной лимит калорий.</summary>
    AwaitingCalorieLimit = 20
}

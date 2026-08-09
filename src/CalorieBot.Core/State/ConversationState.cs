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

    /// <summary>
    /// Жду, пока пользователь нажмёт инлайн-кнопку «на 100 г» / «на порцию целиком» — сам выбор
    /// кнопочный, но текстовый ввод в этом состоянии тоже нужно перехватывать, а не парсить как БЖУ.
    /// </summary>
    AwaitingMealMacrosMode = 2,

    /// <summary>Жду БЖУ нового продукта для записи в дневник (смысл чисел зависит от выбранного режима).</summary>
    AwaitingMealProductMacros = 3,

    /// <summary>Жду вес порции в граммах — только когда БЖУ вводили на 100 г, чтобы пересчитать на порцию.</summary>
    AwaitingMealServingGrams = 4,

    /// <summary>Жду название продукта, который добавляем в избранное.</summary>
    AwaitingFavoriteName = 10,

    /// <summary>Жду выбор режима ввода БЖУ (на 100 г / на порцию целиком) для избранного.</summary>
    AwaitingFavoriteMacrosMode = 11,

    /// <summary>Жду БЖУ продукта для избранного (смысл чисел зависит от выбранного режима).</summary>
    AwaitingFavoriteMacros = 12,

    /// <summary>Жду вес порции в граммах — только когда БЖУ вводили на 100 г.</summary>
    AwaitingFavoriteServingGrams = 13,

    /// <summary>Жду описание порции для избранного (шаг можно пропустить кнопкой; только для режима «на порцию целиком»).</summary>
    AwaitingFavoriteServingSize = 14,

    /// <summary>Жду новый дневной лимит калорий.</summary>
    AwaitingCalorieLimit = 20
}

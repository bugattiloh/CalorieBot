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

    /// <summary>
    /// Жду вес съеденной порции в граммах — либо для нового продукта, введённого на 100 г, либо
    /// для выбранного из избранного продукта с плавающей порцией.
    /// </summary>
    AwaitingMealServingGrams = 4,

    /// <summary>Жду название продукта, который добавляем в избранное.</summary>
    AwaitingFavoriteName = 10,

    /// <summary>Жду выбор типа порции — фиксированная (например, батончик) или на 100 г (например, рис).</summary>
    AwaitingFavoriteMacrosMode = 11,

    /// <summary>Жду БЖУ продукта для избранного (на 100 г или на фиксированную порцию — смотря что выбрали).</summary>
    AwaitingFavoriteMacros = 12,

    /// <summary>Жду описание порции для избранного (шаг можно пропустить кнопкой; только для фиксированной порции).</summary>
    AwaitingFavoriteServingSize = 14,

    /// <summary>Жду новый дневной лимит калорий.</summary>
    AwaitingCalorieLimit = 20,

    /// <summary>Жду выбор режима отслеживания — по калориям или по БЖУ.</summary>
    AwaitingLimitMode = 21,

    /// <summary>Жду новые дневные лимиты по белкам/жирам/углеводам.</summary>
    AwaitingMacroLimits = 22,

    /// <summary>Жду название продукта, который добавляем задним числом в уже закрытый цикл.</summary>
    AwaitingCycleEntryName = 30,

    /// <summary>Жду БЖУ продукта, который добавляем задним числом в уже закрытый цикл.</summary>
    AwaitingCycleEntryMacros = 31,

    /// <summary>Жду название нового элемента «Воды» (жидкости) для избранного.</summary>
    AwaitingWaterName = 40,

    /// <summary>Жду БЖУ нового элемента «Воды» — всегда на 1 литр, шаг типа порции здесь не нужен.</summary>
    AwaitingWaterMacros = 41,

    /// <summary>Жду название нового готового блюда.</summary>
    AwaitingDishName = 50,

    /// <summary>Жду название ингредиента, добавляемого в блюдо.</summary>
    AwaitingDishIngredientName = 51,

    /// <summary>Жду БЖУ ингредиента, добавляемого в блюдо.</summary>
    AwaitingDishIngredientMacros = 52,

    /// <summary>Жду вес плавающей порции избранного продукта, выбранного как ингредиент блюда.</summary>
    AwaitingDishIngredientGrams = 53,

    /// <summary>Жду название подкатегории «Продуктов» — новой или переименовываемой (см. <see cref="ConversationContext.EditingProductCategoryId"/>).</summary>
    AwaitingProductCategoryName = 60,

    /// <summary>Жду, пока пользователь нажмёт «Мужчина»/«Женщина» в калькуляторе нормы КБЖУ.</summary>
    AwaitingCalcSex = 70,

    /// <summary>Жду возраст в калькуляторе нормы КБЖУ.</summary>
    AwaitingCalcAge = 71,

    /// <summary>Жду рост в калькуляторе нормы КБЖУ.</summary>
    AwaitingCalcHeight = 72,

    /// <summary>Жду вес в калькуляторе нормы КБЖУ.</summary>
    AwaitingCalcWeight = 73,

    /// <summary>Жду выбор уровня активности в калькуляторе нормы КБЖУ.</summary>
    AwaitingCalcActivity = 74,

    /// <summary>Жду выбор цели (похудение/поддержание/набор) в калькуляторе нормы КБЖУ.</summary>
    AwaitingCalcGoal = 75
}

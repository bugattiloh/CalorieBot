namespace CalorieBot.Api.Bot.UI;

/// <summary>
/// Коды инлайн-кнопок. Telegram отдаёт не больше 64 байт в callback_data,
/// поэтому использую короткие префиксы вида «pf:12».
/// </summary>
public static class Callbacks
{
    /// <summary>Выбран тип приёма пищи: mt:1..4.</summary>
    public const string MealType = "mt";

    /// <summary>Выбран режим ввода БЖУ для нового продукта дневника: mmm:100 или mmm:full.</summary>
    public const string MealMacrosMode = "mmm";

    /// <summary>Выбран режим ввода БЖУ для избранного: fmm:100 или fmm:full.</summary>
    public const string FavoriteMacrosMode = "fmm";

    /// <summary>Выбран любимый продукт для записи в дневник: pf:{id}.</summary>
    public const string PickFavorite = "pf";

    /// <summary>Листание списка избранного при выборе продукта: pfp:{page}.</summary>
    public const string PickFavoritePage = "pfp";

    /// <summary>Листание списка «Мои продукты»: lfp:{page}.</summary>
    public const string ListPage = "lfp";

    /// <summary>Открыть карточку продукта в «Мои продукты»: fd:{id}.</summary>
    public const string FavoriteDetails = "fd";

    /// <summary>Изменить КБЖУ у уже сохранённого продукта: fem:{id}.</summary>
    public const string FavoriteEditMacros = "fem";

    /// <summary>Переключить тип порции (фиксированная / на 100 г) без изменения КБЖУ: fet:{id}.</summary>
    public const string FavoriteToggleFixed = "fet";

    /// <summary>Листание списка удаления: dfp:{page}.</summary>
    public const string DeletePage = "dfp";

    /// <summary>Запрос на удаление продукта: df:{id}.</summary>
    public const string DeleteFavorite = "df";

    /// <summary>Подтверждение удаления: dfy:{id}.</summary>
    public const string DeleteConfirm = "dfy";

    /// <summary>Сохранить новый продукт в избранное.</summary>
    public const string SaveFavoriteYes = "sfy";

    /// <summary>Не сохранять новый продукт в избранное.</summary>
    public const string SaveFavoriteNo = "sfn";

    /// <summary>Пропустить необязательный шаг с размером порции.</summary>
    public const string SkipServing = "skp";

    /// <summary>«Добавить ещё»: more:meal — ещё приём пищи, more:fav — ещё продукт в избранное.</summary>
    public const string AddMore = "more";

    /// <summary>Возврат в главное меню.</summary>
    public const string ToMenu = "menu";

    /// <summary>Подтверждение начала нового дня (закрытие текущего цикла): ndy:yes или ndy:no.</summary>
    public const string NewDayConfirm = "ndy";

    /// <summary>Листание истории прошлых циклов: chp:{page}.</summary>
    public const string CycleHistoryPage = "chp";

    /// <summary>Выбран режим отслеживания лимита: lm:cal или lm:macro.</summary>
    public const string LimitMode = "lm";

    /// <summary>Ответ на предупреждение о повторе названия приёма пищи: dmc:yes:{mealType}:{entryId} или dmc:no:{mealType}:{entryId}.</summary>
    public const string DuplicateMealConfirm = "dmc";

    /// <summary>Открыть карточку конкретного прошлого цикла: cd:{cycleId}.</summary>
    public const string CycleDetails = "cd";

    /// <summary>Запрос на удаление записи из прошлого цикла: cedr:{cycleId}:{entryId}.</summary>
    public const string CycleEntryDeleteRequest = "cedr";

    /// <summary>Подтверждение удаления записи из прошлого цикла: cedc:{cycleId}:{entryId}.</summary>
    public const string CycleEntryDeleteConfirm = "cedc";

    /// <summary>Начать добавление записи задним числом в прошлый цикл: cae:{cycleId}.</summary>
    public const string CycleAddEntry = "cae";

    /// <summary>Выбран тип приёма пищи для записи, добавляемой в прошлый цикл: cemt:{cycleId}:{mealType}.</summary>
    public const string CycleEntryMealType = "cemt";

    /// <summary>Запрошен отчёт за период из «Мой прогресс»: pr:7 или pr:30.</summary>
    public const string PeriodReport = "pr";

    /// <summary>Листание списка «Вода»: wap:{page}.</summary>
    public const string WaterPage = "wap";

    /// <summary>Начать добавление нового элемента в «Воду».</summary>
    public const string WaterAdd = "wad";

    /// <summary>Листание списка «Готовые блюда»: dip:{page}.</summary>
    public const string DishPage = "dip";

    /// <summary>Начать создание нового блюда.</summary>
    public const string DishAdd = "dad";

    /// <summary>Открыть карточку блюда: dd:{dishId}.</summary>
    public const string DishDetails = "dd";

    /// <summary>Начать добавление ингредиента в блюдо: dai:{dishId}.</summary>
    public const string DishAddIngredient = "dai";

    /// <summary>Запрос на удаление ингредиента: didr:{dishId}:{ingredientId}.</summary>
    public const string DishIngredientDeleteRequest = "didr";

    /// <summary>Подтверждение удаления ингредиента: didc:{dishId}:{ingredientId}.</summary>
    public const string DishIngredientDeleteConfirm = "didc";

    /// <summary>Выбрана подкатегория «Продуктов» для просмотра: pcat:{categoryId}, листание — pcat:{categoryId}:{page}.</summary>
    public const string ProductCategoryPick = "pcat";

    /// <summary>Начать добавление продукта в конкретную подкатегорию: pah:{categoryId}.</summary>
    public const string ProductAddHere = "pah";

    /// <summary>Начать создание новой подкатегории «Продуктов».</summary>
    public const string ProductCategoryNew = "pcn";

    /// <summary>Показать список подкатегорий в режиме управления (переименовать/удалить).</summary>
    public const string ProductCategoryManage = "pcm";

    /// <summary>Выбрана подкатегория в режиме управления: pcmp:{categoryId}.</summary>
    public const string ProductCategoryManagePick = "pcmp";

    /// <summary>Начать переименование подкатегории: pcr:{categoryId}.</summary>
    public const string ProductCategoryRename = "pcr";

    /// <summary>Запрос на удаление подкатегории: pcd:{categoryId}.</summary>
    public const string ProductCategoryDelete = "pcd";

    /// <summary>Подтверждение удаления подкатегории: pcdy:{categoryId}.</summary>
    public const string ProductCategoryDeleteConfirm = "pcdy";

    /// <summary>Открыть выбор подкатегории для переноса продукта: fmc:{favoriteId}.</summary>
    public const string FavoriteMoveCategory = "fmc";

    /// <summary>Подтверждён перенос продукта в другую подкатегорию: fmcc:{favoriteId}:{categoryId|none}.</summary>
    public const string FavoriteMoveCategoryConfirm = "fmcc";

    /// <summary>Вернуться из вложенного экрана «Избранного» (Вода/Блюда/Продукты) на его меню, а не в главное.</summary>
    public const string FavoritesMenu = "favm";

    /// <summary>Вернуться из списка продуктов подкатегории к списку самих подкатегорий.</summary>
    public const string ProductCategoriesShow = "pcs";

    /// <summary>Выбор источника ингредиента для блюда: dis:{dishId}.</summary>
    public const string DishIngredientSource = "dis";

    /// <summary>Начать добавление ингредиента вручную (имя + БЖУ): dic:{dishId}.</summary>
    public const string DishIngredientCustom = "dic";

    /// <summary>Показать список избранных продуктов для добавления в блюдо: diff:{dishId}, листание — diff:{dishId}:{page}.</summary>
    public const string DishIngredientFromFavorite = "diff";

    /// <summary>Выбран конкретный избранный продукт как ингредиент: dipf:{dishId}:{favoriteId}.</summary>
    public const string DishIngredientPickFavorite = "dipf";

    /// <summary>Выбран пол в калькуляторе нормы КБЖУ: lcs:m или lcs:f.</summary>
    public const string LimitCalcSex = "lcs";

    /// <summary>Выбран уровень активности в калькуляторе нормы КБЖУ: lca:sedentary|light|moderate|high|extreme.</summary>
    public const string LimitCalcActivity = "lca";

    /// <summary>Выбрана цель в калькуляторе нормы КБЖУ: lcg:lose|maintain|gain.</summary>
    public const string LimitCalcGoal = "lcg";

    /// <summary>Применить результат расчёта как новый лимит по БЖУ: lcap:lose|maintain|gain.</summary>
    public const string LimitCalcApply = "lcap";

    /// <summary>Кнопка-заглушка (например, счётчик страниц) — ничего не делает.</summary>
    public const string Noop = "noop";

    /// <summary>Собираю callback_data из действия и аргумента.</summary>
    public static string Build(string action, object argument) => $"{action}:{argument}";

    /// <summary>Разбираю callback_data на действие и необязательный аргумент.</summary>
    public static (string Action, string? Argument) Parse(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return (Noop, null);
        }

        var separator = data.IndexOf(':');
        return separator < 0
            ? (data, null)
            : (data[..separator], data[(separator + 1)..]);
    }
}

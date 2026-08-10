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

using CalorieBot.Data.Entities;
using Telegram.Bot.Types.ReplyMarkups;

namespace CalorieBot.Api.Bot.UI;

/// <summary>
/// Все клавиатуры бота. Управление полностью кнопочное, поэтому это, по сути, вся навигация.
/// </summary>
public static class Keyboards
{
    /// <summary>Сколько продуктов показываю на одной странице инлайн-списка.</summary>
    public const int PageSize = 6;

    /// <summary>Главное меню — постоянная клавиатура, её пользователь видит всегда.</summary>
    public static ReplyKeyboardMarkup MainMenu { get; } = new(new[]
    {
        new KeyboardButton[] { Buttons.AddMeal, Buttons.Progress },
        new KeyboardButton[] { Buttons.Favorites, Buttons.Limit },
        new KeyboardButton[] { Buttons.History, Buttons.NewDay },
        new KeyboardButton[] { Buttons.CycleHistory }
    })
    {
        ResizeKeyboard = true,
        IsPersistent = true,
        InputFieldPlaceholder = "Выберите действие на клавиатуре"
    };

    /// <summary>Подменю «Добавить прием пищи».</summary>
    public static ReplyKeyboardMarkup MealMenu { get; } = new(new[]
    {
        new KeyboardButton[] { Buttons.FromFavorites, Buttons.NewProduct },
        new KeyboardButton[] { Buttons.Back }
    })
    {
        ResizeKeyboard = true,
        IsPersistent = true
    };

    /// <summary>Подменю «Любимые продукты».</summary>
    public static ReplyKeyboardMarkup FavoritesMenu { get; } = new(new[]
    {
        new KeyboardButton[] { Buttons.AddFavorite, Buttons.MyProducts },
        new KeyboardButton[] { Buttons.DeleteFavorite },
        new KeyboardButton[] { Buttons.Back }
    })
    {
        ResizeKeyboard = true,
        IsPersistent = true
    };

    /// <summary>Подменю «Дневной лимит».</summary>
    public static ReplyKeyboardMarkup LimitMenu { get; } = new(new[]
    {
        new KeyboardButton[] { Buttons.ChangeLimit, Buttons.CurrentLimit },
        new KeyboardButton[] { Buttons.Back }
    })
    {
        ResizeKeyboard = true,
        IsPersistent = true
    };

    /// <summary>Выбор типа приёма пищи — последний шаг любого добавления еды.</summary>
    public static InlineKeyboardMarkup MealTypes { get; } = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🍳 Завтрак", Callbacks.Build(Callbacks.MealType, (int)MealType.Breakfast)),
            InlineKeyboardButton.WithCallbackData("🍲 Обед", Callbacks.Build(Callbacks.MealType, (int)MealType.Lunch))
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🍝 Ужин", Callbacks.Build(Callbacks.MealType, (int)MealType.Dinner)),
            InlineKeyboardButton.WithCallbackData("🍎 Перекус", Callbacks.Build(Callbacks.MealType, (int)MealType.Snack))
        },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню", Callbacks.ToMenu) }
    });

    /// <summary>
    /// Выбор режима ввода БЖУ. <paramref name="action"/> — <see cref="Callbacks.MealMacrosMode"/>
    /// или <see cref="Callbacks.FavoriteMacrosMode"/>, в зависимости от того, где идёт диалог.
    /// </summary>
    public static InlineKeyboardMarkup MacrosModeChoice(string action) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📏 На 100 г продукта", Callbacks.Build(action, "100")) },
        new[] { InlineKeyboardButton.WithCallbackData("🍽 На порцию целиком", Callbacks.Build(action, "full")) }
    });

    /// <summary>Предложение сохранить только что введённый продукт в избранное.</summary>
    public static InlineKeyboardMarkup SaveFavoriteConfirm { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("✅ Сохранить в избранное", Callbacks.SaveFavoriteYes) },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Нет, спасибо", Callbacks.SaveFavoriteNo) }
    });

    /// <summary>Что делать после записи приёма пищи.</summary>
    public static InlineKeyboardMarkup AfterMealLogged { get; } = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🔄 Добавить еще", Callbacks.Build(Callbacks.AddMore, "meal")),
            InlineKeyboardButton.WithCallbackData("🔙 В меню", Callbacks.ToMenu)
        }
    });

    /// <summary>Что делать после сохранения продукта в избранное.</summary>
    public static InlineKeyboardMarkup AfterFavoriteSaved { get; } = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🔄 Добавить еще", Callbacks.Build(Callbacks.AddMore, "fav")),
            InlineKeyboardButton.WithCallbackData("🔙 В меню", Callbacks.ToMenu)
        }
    });

    /// <summary>Шаг с размером порции необязательный — даю возможность его пропустить.</summary>
    public static InlineKeyboardMarkup SkipServingSize { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("⏭ Пропустить", Callbacks.SkipServing) }
    });

    /// <summary>Подтверждение начала нового дня — сброс цикла случайным тапом быть не должен.</summary>
    public static InlineKeyboardMarkup NewDayConfirm { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🆕 Да, начать новый день", Callbacks.Build(Callbacks.NewDayConfirm, "yes")) },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", Callbacks.Build(Callbacks.NewDayConfirm, "no")) }
    });

    /// <summary>Одинокая кнопка возврата в меню — вешаю её на «тупиковые» сообщения.</summary>
    public static InlineKeyboardMarkup ToMenuOnly { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню", Callbacks.ToMenu) }
    });

    /// <summary>Подтверждение удаления продукта из избранного.</summary>
    public static InlineKeyboardMarkup DeleteConfirm(int favoriteId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", Callbacks.Build(Callbacks.DeleteConfirm, favoriteId)) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.Build(Callbacks.DeletePage, 0)) }
    });

    /// <summary>
    /// Постранично собираю список продуктов: по кнопке на продукт плюс строка навигации.
    /// <paramref name="itemAction"/> — что делать при выборе продукта (выбрать или удалить),
    /// <paramref name="pageAction"/> — код кнопок листания.
    /// </summary>
    public static InlineKeyboardMarkup ProductPage(
        IReadOnlyList<FavoriteProduct> products,
        int page,
        string itemAction,
        string pageAction,
        Func<FavoriteProduct, string> labelFactory)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(products.Count / (double)PageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        var rows = new List<InlineKeyboardButton[]>();

        foreach (var product in products.Skip(page * PageSize).Take(PageSize))
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(labelFactory(product), Callbacks.Build(itemAction, product.Id))
            });
        }

        rows.AddRange(NavigationRows(page, totalPages, pageAction));
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Строки навигации для постраничных списков без выбора элементов.</summary>
    public static InlineKeyboardMarkup PageNavigation(int page, int totalPages, string pageAction) =>
        new(NavigationRows(page, totalPages, pageAction));

    private static List<InlineKeyboardButton[]> NavigationRows(int page, int totalPages, string pageAction)
    {
        var rows = new List<InlineKeyboardButton[]>();

        // Стрелки показываю только когда листать реально есть куда.
        if (totalPages > 1)
        {
            var navigation = new List<InlineKeyboardButton>();

            if (page > 0)
            {
                navigation.Add(InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.Build(pageAction, page - 1)));
            }

            navigation.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", Callbacks.Noop));

            if (page < totalPages - 1)
            {
                navigation.Add(InlineKeyboardButton.WithCallbackData("Вперед ▶️", Callbacks.Build(pageAction, page + 1)));
            }

            rows.Add(navigation.ToArray());
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню", Callbacks.ToMenu) });
        return rows;
    }
}

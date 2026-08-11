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

    /// <summary>Сколько прошлых циклов показываю на одной странице «Истории» — буквально 7 штук, не календарная неделя.</summary>
    public const int CyclePageSize = 7;

    /// <summary>Главное меню — постоянная клавиатура, её пользователь видит всегда.</summary>
    public static ReplyKeyboardMarkup MainMenu { get; } = new(new[]
    {
        new KeyboardButton[] { Buttons.AddMeal, Buttons.Progress },
        new KeyboardButton[] { Buttons.Favorites, Buttons.Limit },
        new KeyboardButton[] { Buttons.NewDay, Buttons.CycleHistory }
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

    /// <summary>Подменю «Избранное» — три группы вместо одного огромного списка.</summary>
    public static ReplyKeyboardMarkup FavoritesMenu { get; } = new(new[]
    {
        new KeyboardButton[] { Buttons.Water, Buttons.Dishes },
        new KeyboardButton[] { Buttons.Products },
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

    /// <summary>Выбор режима ввода БЖУ для записи в дневник (<see cref="Callbacks.MealMacrosMode"/>).</summary>
    public static InlineKeyboardMarkup MacrosModeChoice(string action) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📏 На 100 г продукта", Callbacks.Build(action, "100")) },
        new[] { InlineKeyboardButton.WithCallbackData("🍽 На порцию целиком", Callbacks.Build(action, "full")) }
    });

    /// <summary>
    /// Выбор типа порции для избранного — та же механика (100/full), что и <see cref="MacrosModeChoice"/>,
    /// но формулировка подчёркивает, что это постоянное свойство продукта, а не разовое удобство ввода.
    /// </summary>
    public static InlineKeyboardMarkup FavoriteServingModeChoice { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🍽 Фиксированная порция", Callbacks.Build(Callbacks.FavoriteMacrosMode, "full")) },
        new[] { InlineKeyboardButton.WithCallbackData("📏 На 100 г (порция плавающая)", Callbacks.Build(Callbacks.FavoriteMacrosMode, "100")) }
    });

    /// <summary>
    /// Действия в карточке избранного продукта — набор зависит от группы: у «Воды» нет типа порции
    /// и переноса между подкатегориями, у «Продуктов» есть оба.
    /// </summary>
    public static InlineKeyboardMarkup FavoriteDetailsActions(FavoriteProduct product)
    {
        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Изменить КБЖУ", Callbacks.Build(Callbacks.FavoriteEditMacros, product.Id)) }
        };

        if (product.CategoryKind == FavoriteCategoryKind.Product)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    product.IsFixedServing ? "🔁 Сделать порцию плавающей" : "🔁 Сделать порцию фиксированной",
                    Callbacks.Build(Callbacks.FavoriteToggleFixed, product.Id))
            });
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("📂 В другую подкатегорию", Callbacks.Build(Callbacks.FavoriteMoveCategory, product.Id))
            });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🗑 Удалить", Callbacks.Build(Callbacks.DeleteFavorite, product.Id)) });

        var backAction = product.CategoryKind == FavoriteCategoryKind.Water
            ? Callbacks.Build(Callbacks.WaterPage, 0)
            : Callbacks.Build(Callbacks.ProductCategoryPick, product.ProductCategoryId ?? 0);
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", backAction) });

        return new InlineKeyboardMarkup(rows);
    }

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

    /// <summary>Выбор режима отслеживания лимита: по калориям, по БЖУ напрямую или расчётом по формуле.</summary>
    public static InlineKeyboardMarkup LimitModeChoice { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📐 По калориям", Callbacks.Build(Callbacks.LimitMode, "cal")) },
        new[] { InlineKeyboardButton.WithCallbackData("🥩 По БЖУ", Callbacks.Build(Callbacks.LimitMode, "macro")) },
        new[] { InlineKeyboardButton.WithCallbackData("🧮 Рассчитать по формуле", Callbacks.Build(Callbacks.LimitMode, "calc")) }
    });

    /// <summary>Первый шаг калькулятора нормы КБЖУ — пол (нужен формуле Миффлина — Сан-Жеора).</summary>
    public static InlineKeyboardMarkup CalcSexChoice { get; } = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("👨 Мужчина", Callbacks.Build(Callbacks.LimitCalcSex, "m")),
            InlineKeyboardButton.WithCallbackData("👩 Женщина", Callbacks.Build(Callbacks.LimitCalcSex, "f"))
        }
    });

    /// <summary>Выбор уровня активности — коэффициент для перевода базового обмена в суточную норму.</summary>
    public static InlineKeyboardMarkup CalcActivityChoice { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🛋 Сидячий образ жизни", Callbacks.Build(Callbacks.LimitCalcActivity, "sedentary")) },
        new[] { InlineKeyboardButton.WithCallbackData("🚶 Лёгкая (1–2 трен/нед)", Callbacks.Build(Callbacks.LimitCalcActivity, "light")) },
        new[] { InlineKeyboardButton.WithCallbackData("🏃 Средняя (3–5 трен/нед)", Callbacks.Build(Callbacks.LimitCalcActivity, "moderate")) },
        new[] { InlineKeyboardButton.WithCallbackData("🏋 Высокая (тренировки ежедневно)", Callbacks.Build(Callbacks.LimitCalcActivity, "high")) },
        new[] { InlineKeyboardButton.WithCallbackData("🔥 Экстремальная (проф. спорт)", Callbacks.Build(Callbacks.LimitCalcActivity, "extreme")) }
    });

    /// <summary>Выбор цели — определяет и корректировку калорийности, и норму белка на кг веса.</summary>
    public static InlineKeyboardMarkup CalcGoalChoice { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📉 Похудение (−15% от нормы)", Callbacks.Build(Callbacks.LimitCalcGoal, "lose")) },
        new[] { InlineKeyboardButton.WithCallbackData("⚖️ Поддержание веса", Callbacks.Build(Callbacks.LimitCalcGoal, "maintain")) },
        new[] { InlineKeyboardButton.WithCallbackData("📈 Набор массы (+15% от нормы)", Callbacks.Build(Callbacks.LimitCalcGoal, "gain")) }
    });

    /// <summary>Применить рассчитанный лимит или отменить расчёт.</summary>
    public static InlineKeyboardMarkup CalcResultActions(string goal) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("✅ Применить как лимит по БЖУ", Callbacks.Build(Callbacks.LimitCalcApply, goal)) },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", Callbacks.ToMenu) }
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

    /// <summary>Выбор периода отчёта под экраном «Мой прогресс».</summary>
    public static InlineKeyboardMarkup ProgressReportChoice { get; } = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("📈 За неделю", Callbacks.Build(Callbacks.PeriodReport, 7)),
            InlineKeyboardButton.WithCallbackData("📈 За месяц", Callbacks.Build(Callbacks.PeriodReport, 30))
        }
    });

    /// <summary>Продукт с таким названием уже записан в этом цикле — предлагаю заменить или добавить отдельно.</summary>
    public static InlineKeyboardMarkup DuplicateMealConfirm(int mealType, int entryId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("♻️ Заменить предыдущую запись", Callbacks.Build(Callbacks.DuplicateMealConfirm, $"yes:{mealType}:{entryId}")) },
        new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить как новую", Callbacks.Build(Callbacks.DuplicateMealConfirm, $"no:{mealType}:{entryId}")) },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", Callbacks.ToMenu) }
    });

    /// <summary>Подтверждение удаления продукта из избранного.</summary>
    public static InlineKeyboardMarkup DeleteConfirm(int favoriteId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", Callbacks.Build(Callbacks.DeleteConfirm, favoriteId)) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.Build(Callbacks.DeletePage, 0)) }
    });

    /// <summary>Список прошлых циклов страницей — тап по циклу открывает его карточку для редактирования.</summary>
    public static InlineKeyboardMarkup CycleHistoryButtons(IReadOnlyList<CalorieCycle> cycles, int page, int pageSize, int totalPages)
    {
        var rows = new List<InlineKeyboardButton[]>();
        var index = page * pageSize + 1;

        foreach (var cycle in cycles)
        {
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"✏️ Открыть цикл {index}", Callbacks.Build(Callbacks.CycleDetails, cycle.Id)) });
            index++;
        }

        rows.AddRange(NavigationRows(page, totalPages, Callbacks.CycleHistoryPage));
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// Карточка прошлого цикла: каждая запись — отдельная кнопка удаления, плюс добавление новой записи.
    /// </summary>
    public static InlineKeyboardMarkup CycleDetailsActions(
        int cycleId, IReadOnlyList<FoodLogEntry> entries, Func<FoodLogEntry, string> labelFactory)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var entry in entries.OrderBy(e => e.LoggedAt))
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(labelFactory(entry), Callbacks.Build(Callbacks.CycleEntryDeleteRequest, $"{cycleId}:{entry.Id}"))
            });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить запись", Callbacks.Build(Callbacks.CycleAddEntry, cycleId)) });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ К истории", Callbacks.Build(Callbacks.CycleHistoryPage, 0)) });
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Подтверждение удаления записи из прошлого цикла.</summary>
    public static InlineKeyboardMarkup CycleEntryDeleteConfirm(int cycleId, int entryId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", Callbacks.Build(Callbacks.CycleEntryDeleteConfirm, $"{cycleId}:{entryId}")) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.Build(Callbacks.CycleDetails, cycleId)) }
    });

    /// <summary>Выбор типа приёма пищи для записи, добавляемой задним числом в прошлый цикл.</summary>
    public static InlineKeyboardMarkup CycleEntryMealTypes(int cycleId) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🍳 Завтрак", Callbacks.Build(Callbacks.CycleEntryMealType, $"{cycleId}:{(int)MealType.Breakfast}")),
            InlineKeyboardButton.WithCallbackData("🍲 Обед", Callbacks.Build(Callbacks.CycleEntryMealType, $"{cycleId}:{(int)MealType.Lunch}"))
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🍝 Ужин", Callbacks.Build(Callbacks.CycleEntryMealType, $"{cycleId}:{(int)MealType.Dinner}")),
            InlineKeyboardButton.WithCallbackData("🍎 Перекус", Callbacks.Build(Callbacks.CycleEntryMealType, $"{cycleId}:{(int)MealType.Snack}"))
        },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Отмена", Callbacks.Build(Callbacks.CycleDetails, cycleId)) }
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

    /// <summary>
    /// Список продуктов одной группы избранного (Вода/Готовые блюда/подкатегория Продуктов) плюс кнопка
    /// добавления. Кнопка возврата — своя (<paramref name="backLabel"/>/<paramref name="backActionData"/>),
    /// а не общий «В меню»: экран вложенный, должен возвращать на шаг назад, а не сразу в главное меню.
    /// </summary>
    public static InlineKeyboardMarkup CategoryItemList(
        IReadOnlyList<FavoriteProduct> products,
        int page,
        string itemAction,
        string pageAction,
        string addActionData,
        string addLabel,
        string backLabel,
        string backActionData,
        Func<FavoriteProduct, string> labelFactory)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(products.Count / (double)PageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        var rows = new List<InlineKeyboardButton[]>();

        foreach (var product in products.Skip(page * PageSize).Take(PageSize))
        {
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(labelFactory(product), Callbacks.Build(itemAction, product.Id)) });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(addLabel, addActionData) });
        rows.AddRange(NavigationRows(page, totalPages, pageAction, backLabel, backActionData));
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// Подкатегории «Продуктов»: в обычном режиме — тап открывает список продуктов, в режиме управления —
    /// карточку с переименованием/удалением. Кнопка возврата: из обычного режима — в меню «Избранное»,
    /// из режима управления — обратно к обычному списку подкатегорий.
    /// </summary>
    public static InlineKeyboardMarkup ProductCategoriesList(IReadOnlyList<ProductCategory> categories, bool hasUncategorized, bool manageMode)
    {
        var itemAction = manageMode ? Callbacks.ProductCategoryManagePick : Callbacks.ProductCategoryPick;
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var category in categories)
        {
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(category.Name, Callbacks.Build(itemAction, category.Id)) });
        }

        // 0 — служебный «без подкатегории»: настоящие Id подкатегорий всегда положительные.
        if (hasUncategorized && !manageMode)
        {
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("📦 Без категории", Callbacks.Build(itemAction, 0)) });
        }

        rows.Add(manageMode
            ? new[] { InlineKeyboardButton.WithCallbackData("➕ Новая категория", Callbacks.ProductCategoryNew) }
            : new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Новая категория", Callbacks.ProductCategoryNew),
                InlineKeyboardButton.WithCallbackData("✏️ Управлять", Callbacks.ProductCategoryManage)
            });

        rows.Add(manageMode
            ? new[] { InlineKeyboardButton.WithCallbackData("◀️ К подкатегориям", Callbacks.ProductCategoriesShow) }
            : new[] { InlineKeyboardButton.WithCallbackData("◀️ Избранное", Callbacks.FavoritesMenu) });

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Действия с подкатегорией в режиме управления.</summary>
    public static InlineKeyboardMarkup ProductCategoryManageActions(int categoryId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("✏️ Переименовать", Callbacks.Build(Callbacks.ProductCategoryRename, categoryId)) },
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Удалить", Callbacks.Build(Callbacks.ProductCategoryDelete, categoryId)) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.ProductCategoryManage) }
    });

    /// <summary>Подтверждение удаления подкатегории — продукты внутри не удаляются, только теряют подкатегорию.</summary>
    public static InlineKeyboardMarkup ProductCategoryDeleteConfirm(int categoryId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", Callbacks.Build(Callbacks.ProductCategoryDeleteConfirm, categoryId)) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.Build(Callbacks.ProductCategoryManagePick, categoryId)) }
    });

    /// <summary>Выбор подкатегории, в которую «перетаскивается» продукт.</summary>
    public static InlineKeyboardMarkup FavoriteMoveCategoryChoice(int favoriteId, IReadOnlyList<ProductCategory> categories)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var category in categories)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(category.Name, Callbacks.Build(Callbacks.FavoriteMoveCategoryConfirm, $"{favoriteId}:{category.Id}"))
            });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("📦 Без категории", Callbacks.Build(Callbacks.FavoriteMoveCategoryConfirm, $"{favoriteId}:0")) });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", Callbacks.Build(Callbacks.FavoriteDetails, favoriteId)) });
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Карточка блюда: каждый ингредиент — отдельная кнопка удаления, плюс добавление и удаление блюда целиком.</summary>
    public static InlineKeyboardMarkup DishDetailsActions(
        int dishId, IReadOnlyList<DishIngredient> ingredients, Func<DishIngredient, string> labelFactory)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var ingredient in ingredients)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(labelFactory(ingredient), Callbacks.Build(Callbacks.DishIngredientDeleteRequest, $"{dishId}:{ingredient.Id}"))
            });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить ингредиент", Callbacks.Build(Callbacks.DishAddIngredient, dishId)) });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🗑 Удалить блюдо целиком", Callbacks.Build(Callbacks.DeleteFavorite, dishId)) });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ К списку блюд", Callbacks.Build(Callbacks.DishPage, 0)) });
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Подтверждение удаления ингредиента блюда.</summary>
    public static InlineKeyboardMarkup DishIngredientDeleteConfirm(int dishId, int ingredientId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", Callbacks.Build(Callbacks.DishIngredientDeleteConfirm, $"{dishId}:{ingredientId}")) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.Build(Callbacks.DishDetails, dishId)) }
    });

    /// <summary>Выбор источника ингредиента для блюда — из избранного или свой продукт.</summary>
    public static InlineKeyboardMarkup DishIngredientSourceChoice(int dishId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("⭐ Из избранного", Callbacks.Build(Callbacks.DishIngredientFromFavorite, dishId)) },
        new[] { InlineKeyboardButton.WithCallbackData("🔍 Свой продукт", Callbacks.Build(Callbacks.DishIngredientCustom, dishId)) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", Callbacks.Build(Callbacks.DishDetails, dishId)) }
    });

    /// <summary>Список избранных продуктов для выбора в качестве ингредиента блюда.</summary>
    public static InlineKeyboardMarkup DishIngredientFavoritePicker(
        IReadOnlyList<FavoriteProduct> products, int page, int dishId, Func<FavoriteProduct, string> labelFactory)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(products.Count / (double)PageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        var rows = new List<InlineKeyboardButton[]>();

        foreach (var product in products.Skip(page * PageSize).Take(PageSize))
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(labelFactory(product), Callbacks.Build(Callbacks.DishIngredientPickFavorite, $"{dishId}:{product.Id}"))
            });
        }

        rows.AddRange(NavigationRows(
            page, totalPages, Callbacks.Build(Callbacks.DishIngredientFromFavorite, dishId),
            "◀️ Назад", Callbacks.Build(Callbacks.DishIngredientSource, dishId)));
        return new InlineKeyboardMarkup(rows);
    }

    private static List<InlineKeyboardButton[]> NavigationRows(
        int page, int totalPages, string pageAction, string backLabel = "🔙 В меню", string backActionData = Callbacks.ToMenu)
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

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(backLabel, backActionData) });
        return rows;
    }
}

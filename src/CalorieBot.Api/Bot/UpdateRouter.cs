using CalorieBot.Api.Bot.Scenarios;
using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CalorieBot.Api.Bot;

/// <summary>
/// Точка входа для одного апдейта: разбираю, что нажал пользователь, и отдаю в нужный сценарий.
/// Живёт в scope апдейта вместе с DbContext.
/// </summary>
public sealed class UpdateRouter
{
    private readonly BotMessenger _messenger;
    private readonly IUserService _users;
    private readonly IConversationStateStore _states;
    private readonly MealScenario _meal;
    private readonly FavoritesScenario _favorites;
    private readonly DishScenario _dish;
    private readonly LimitScenario _limit;
    private readonly ProgressScenario _progress;
    private readonly CycleScenario _cycle;
    private readonly ILogger<UpdateRouter> _logger;

    public UpdateRouter(
        BotMessenger messenger,
        IUserService users,
        IConversationStateStore states,
        MealScenario meal,
        FavoritesScenario favorites,
        DishScenario dish,
        LimitScenario limit,
        ProgressScenario progress,
        CycleScenario cycle,
        ILogger<UpdateRouter> logger)
    {
        _messenger = messenger;
        _users = users;
        _states = states;
        _meal = meal;
        _favorites = favorites;
        _dish = dish;
        _limit = limit;
        _progress = progress;
        _cycle = cycle;
        _logger = logger;
    }

    public Task RouteAsync(Update update, CancellationToken ct) => update switch
    {
        { Message: { } message } => OnMessageAsync(message, ct),
        { CallbackQuery: { } query } => OnCallbackAsync(query, ct),
        // Остальные типы апдейтов я не запрашиваю, но на всякий случай не падаю на них.
        _ => LogSkipped(update)
    };

    private Task LogSkipped(Update update)
    {
        _logger.LogDebug("Пропускаю апдейт {UpdateId} типа {UpdateType}", update.Id, update.Type);
        return Task.CompletedTask;
    }

    private async Task OnMessageAsync(Message message, CancellationToken ct)
    {
        // Бот личный: групповые чаты и сообщения от других ботов игнорирую.
        if (message.From is null || message.From.IsBot || message.Chat.Type != ChatType.Private)
        {
            return;
        }

        var userId = message.From.Id;
        var chatId = message.Chat.Id;

        var user = await _users.GetOrCreateAsync(userId, message.From.Username, message.From.FirstName, ct);
        _states.Touch(userId);

        var text = message.Text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            // Фото, стикеры, голосовые — всё это я не умею разбирать, возвращаю к кнопкам.
            await _messenger.SendAsync(chatId, Texts.OnlyButtons, Keyboards.MainMenu, ct);
            return;
        }

        // Единственная команда бота — инициализация.
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            _states.Clear(userId);
            await _messenger.SendAsync(
                chatId,
                Texts.Greeting(user.FirstName, user.DailyCalorieLimit, user.GoalSetAt is null),
                Keyboards.MainMenu,
                ct);
            return;
        }

        // Кнопки меню имеют приоритет над шагом диалога: так пользователь всегда может выйти из него.
        if (await TryHandleMenuButtonAsync(chatId, userId, text, ct))
        {
            return;
        }

        var context = _states.Get(userId);

        switch (context.State)
        {
            case ConversationState.AwaitingMealProductName:
                await _meal.HandleNameAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingMealProductMacros:
                await _meal.HandleMacrosAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingMealServingGrams:
                await _meal.HandleServingGramsAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingFavoriteName:
                await _favorites.HandleNameAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingFavoriteMacros:
                await _favorites.HandleMacrosAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingFavoriteServingSize:
                await _favorites.HandleServingSizeAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingCalorieLimit:
                await _limit.HandleNewLimitAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingMacroLimits:
                await _limit.HandleNewMacroLimitsAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingCycleEntryName:
                await _cycle.HandleEntryNameAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingCycleEntryMacros:
                await _cycle.HandleEntryMacrosAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingWaterName:
                await _favorites.HandleWaterNameAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingWaterMacros:
                await _favorites.HandleWaterMacrosAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingProductCategoryName:
                await _favorites.HandleCategoryNameAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingDishName:
                await _dish.HandleNameAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingDishIngredientName:
                await _dish.HandleIngredientNameAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingDishIngredientMacros:
                await _dish.HandleIngredientMacrosAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingDishIngredientGrams:
                await _dish.HandleIngredientGramsAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingCalcAge:
                await _limit.HandleCalcAgeAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingCalcHeight:
                await _limit.HandleCalcHeightAsync(chatId, userId, text, ct);
                return;

            case ConversationState.AwaitingCalcWeight:
                await _limit.HandleCalcWeightAsync(chatId, userId, text, ct);
                return;

            default:
                await _messenger.SendAsync(chatId, Texts.OnlyButtons, Keyboards.MainMenu, ct);
                return;
        }
    }

    /// <summary>Обрабатываю нажатия обычной клавиатуры. Возвращаю true, если текст был кнопкой меню.</summary>
    private async Task<bool> TryHandleMenuButtonAsync(long chatId, long userId, string text, CancellationToken ct)
    {
        switch (text)
        {
            // Главное меню.
            case Buttons.AddMeal:
                await _meal.ShowMenuAsync(chatId, userId, ct);
                return true;

            case Buttons.Progress:
                await _progress.ShowProgressAsync(chatId, userId, ct);
                return true;

            case Buttons.Favorites:
                await _favorites.ShowMenuAsync(chatId, userId, ct);
                return true;

            case Buttons.Limit:
                await _limit.ShowMenuAsync(chatId, userId, ct);
                return true;

            case Buttons.NewDay:
                await _cycle.ShowNewDayConfirmAsync(chatId, userId, ct);
                return true;

            case Buttons.CycleHistory:
                await _cycle.ShowHistoryAsync(chatId, userId, page: 0, editMessageId: null, ct);
                return true;

            // Подменю «Добавить прием пищи».
            case Buttons.FromFavorites:
                await _meal.ShowFavoritesAsync(chatId, userId, page: 0, editMessageId: null, ct);
                return true;

            case Buttons.NewProduct:
                await _meal.StartNewProductAsync(chatId, userId, ct);
                return true;

            // Подменю «Избранное» — три группы.
            case Buttons.Water:
                await _favorites.ShowWaterListAsync(chatId, userId, page: 0, editMessageId: null, ct);
                return true;

            case Buttons.Dishes:
                await _dish.ShowListAsync(chatId, userId, page: 0, editMessageId: null, ct);
                return true;

            case Buttons.Products:
                await _favorites.ShowProductCategoriesAsync(chatId, userId, editMessageId: null, manageMode: false, ct);
                return true;

            case Buttons.DeleteFavorite:
                await _favorites.ShowDeleteListAsync(chatId, userId, page: 0, editMessageId: null, ct);
                return true;

            // Подменю «Дневной лимит».
            case Buttons.ChangeLimit:
                await _limit.StartChangeAsync(chatId, userId, ct);
                return true;

            case Buttons.CurrentLimit:
                await _limit.ShowCurrentAsync(chatId, userId, ct);
                return true;

            // Выход из любого подменю.
            case Buttons.Back:
                _states.Get(userId).Reset();
                await _messenger.SendAsync(chatId, "🔙 Главное меню", Keyboards.MainMenu, ct);
                return true;

            default:
                return false;
        }
    }

    private async Task OnCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        var userId = query.From.Id;
        await _users.GetOrCreateAsync(userId, query.From.Username, query.From.FirstName, ct);
        _states.Touch(userId);

        var (action, argument) = Callbacks.Parse(query.Data);

        // Почти всем обработчикам нужно сообщение, к которому прикреплена клавиатура.
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        var chatId = query.Message.Chat.Id;
        var messageId = query.Message.MessageId;

        switch (action)
        {
            case Callbacks.MealType:
                await _meal.HandleMealTypeAsync(query, argument, ct);
                return;

            case Callbacks.DuplicateMealConfirm:
                await _meal.HandleDuplicateMealConfirmAsync(query, argument, ct);
                return;

            case Callbacks.MealMacrosMode:
                await _meal.HandleMacrosModeAsync(query, argument, ct);
                return;

            case Callbacks.FavoriteMacrosMode:
                await _favorites.HandleMacrosModeAsync(query, argument, ct);
                return;

            case Callbacks.LimitMode:
                await _limit.HandleLimitModeAsync(query, argument, ct);
                return;

            case Callbacks.LimitCalcSex:
                await _limit.HandleCalcSexAsync(query, argument, ct);
                return;

            case Callbacks.LimitCalcActivity:
                await _limit.HandleCalcActivityAsync(query, argument, ct);
                return;

            case Callbacks.LimitCalcGoal:
                await _limit.HandleCalcGoalAsync(query, argument, ct);
                return;

            case Callbacks.LimitCalcApply:
                await _limit.HandleCalcApplyAsync(query, argument, ct);
                return;

            case Callbacks.FavoriteDetails:
                await _favorites.ShowDetailsAsync(query, argument, ct);
                return;

            case Callbacks.FavoriteEditMacros:
                await _favorites.StartEditMacrosAsync(query, argument, ct);
                return;

            case Callbacks.FavoriteToggleFixed:
                await _favorites.HandleToggleFixedServingAsync(query, argument, ct);
                return;

            case Callbacks.NewDayConfirm:
                await _cycle.HandleNewDayConfirmAsync(query, argument, ct);
                return;

            case Callbacks.CycleHistoryPage:
                await _messenger.AnswerAsync(query, ct: ct);
                await _cycle.ShowHistoryAsync(chatId, userId, ParsePage(argument), messageId, ct);
                return;

            case Callbacks.CycleDetails:
                await _cycle.HandleCycleDetailsAsync(query, argument, ct);
                return;

            case Callbacks.CycleEntryDeleteRequest:
                await _cycle.HandleEntryDeleteRequestAsync(query, argument, ct);
                return;

            case Callbacks.CycleEntryDeleteConfirm:
                await _cycle.HandleEntryDeleteConfirmAsync(query, argument, ct);
                return;

            case Callbacks.CycleAddEntry:
                await _cycle.StartAddEntryAsync(query, argument, ct);
                return;

            case Callbacks.CycleEntryMealType:
                await _cycle.HandleEntryMealTypeAsync(query, argument, ct);
                return;

            case Callbacks.PeriodReport:
                await _progress.HandlePeriodReportAsync(query, argument, ct);
                return;

            case Callbacks.PickFavorite:
                await _meal.HandleFavoritePickedAsync(query, argument, ct);
                return;

            case Callbacks.PickFavoritePage:
                await _messenger.AnswerAsync(query, ct: ct);
                await _meal.ShowFavoritesAsync(chatId, userId, ParsePage(argument), messageId, ct);
                return;

            case Callbacks.WaterPage:
                await _messenger.AnswerAsync(query, ct: ct);
                await _favorites.ShowWaterListAsync(chatId, userId, ParsePage(argument), messageId, ct);
                return;

            case Callbacks.WaterAdd:
                await _favorites.StartAddWaterAsync(query, ct);
                return;

            case Callbacks.DishPage:
                await _messenger.AnswerAsync(query, ct: ct);
                await _dish.ShowListAsync(chatId, userId, ParsePage(argument), messageId, ct);
                return;

            case Callbacks.DishAdd:
                await _dish.StartCreateAsync(query, ct);
                return;

            case Callbacks.DishDetails:
                await _dish.ShowDetailsAsync(query, argument, ct);
                return;

            case Callbacks.DishAddIngredient:
                await _dish.StartAddIngredientAsync(query, argument, ct);
                return;

            case Callbacks.DishIngredientSource:
                await _dish.StartAddIngredientAsync(query, argument, ct);
                return;

            case Callbacks.DishIngredientCustom:
                await _dish.StartAddCustomIngredientAsync(query, argument, ct);
                return;

            case Callbacks.DishIngredientFromFavorite:
                await _dish.ShowFavoriteIngredientPickerAsync(query, argument, ct);
                return;

            case Callbacks.DishIngredientPickFavorite:
                await _dish.HandlePickFavoriteIngredientAsync(query, argument, ct);
                return;

            case Callbacks.DishIngredientDeleteRequest:
                await _dish.HandleIngredientDeleteRequestAsync(query, argument, ct);
                return;

            case Callbacks.DishIngredientDeleteConfirm:
                await _dish.HandleIngredientDeleteConfirmAsync(query, argument, ct);
                return;

            case Callbacks.ProductCategoryPick:
                await _favorites.HandleProductCategoryPickAsync(query, argument, ct);
                return;

            case Callbacks.ProductAddHere:
                await _favorites.StartAddProductAsync(query, argument, ct);
                return;

            case Callbacks.ProductCategoryNew:
                await _favorites.StartCreateCategoryAsync(query, ct);
                return;

            case Callbacks.ProductCategoryManage:
                await _favorites.ShowCategoryManageListAsync(query, ct);
                return;

            case Callbacks.ProductCategoryManagePick:
                await _favorites.ShowCategoryManageCardAsync(query, argument, ct);
                return;

            case Callbacks.ProductCategoryRename:
                await _favorites.StartRenameCategoryAsync(query, argument, ct);
                return;

            case Callbacks.ProductCategoryDelete:
                await _favorites.HandleCategoryDeleteRequestAsync(query, argument, ct);
                return;

            case Callbacks.ProductCategoryDeleteConfirm:
                await _favorites.HandleCategoryDeleteConfirmAsync(query, argument, ct);
                return;

            case Callbacks.FavoriteMoveCategory:
                await _favorites.StartMoveCategoryAsync(query, argument, ct);
                return;

            case Callbacks.FavoriteMoveCategoryConfirm:
                await _favorites.HandleMoveCategoryConfirmAsync(query, argument, ct);
                return;

            case Callbacks.FavoritesMenu:
                await _favorites.ShowMenuFromCallbackAsync(query, ct);
                return;

            case Callbacks.ProductCategoriesShow:
                await _favorites.ShowProductCategoriesFromCallbackAsync(query, ct);
                return;

            case Callbacks.DeletePage:
                await _messenger.AnswerAsync(query, ct: ct);
                await _favorites.ShowDeleteListAsync(chatId, userId, ParsePage(argument), messageId, ct);
                return;

            case Callbacks.DeleteFavorite:
                await _favorites.HandleDeleteRequestAsync(query, argument, ct);
                return;

            case Callbacks.DeleteConfirm:
                await _favorites.HandleDeleteConfirmAsync(query, argument, ct);
                return;

            case Callbacks.SaveFavoriteYes:
                await _meal.HandleSaveFavoriteAnswerAsync(query, save: true, ct);
                return;

            case Callbacks.SaveFavoriteNo:
                await _meal.HandleSaveFavoriteAnswerAsync(query, save: false, ct);
                return;

            case Callbacks.SkipServing:
                await _favorites.HandleSkipServingSizeAsync(query, ct);
                return;

            case Callbacks.AddMore:
                await _messenger.AnswerAsync(query, ct: ct);
                await _messenger.RemoveKeyboardAsync(chatId, messageId, ct);

                if (string.Equals(argument, "fav", StringComparison.Ordinal))
                {
                    // Продукт мог быть добавлен в любую из трёх групп — однозначно повторить
                    // тот же shortcut нельзя, поэтому возвращаю в меню «Избранное» выбрать заново.
                    await _favorites.ShowMenuAsync(chatId, userId, ct);
                }
                else
                {
                    await _meal.ShowMenuAsync(chatId, userId, ct);
                }

                return;

            case Callbacks.ToMenu:
                _states.Get(userId).Reset();
                await _messenger.AnswerAsync(query, ct: ct);
                await _messenger.RemoveKeyboardAsync(chatId, messageId, ct);
                await _messenger.SendAsync(chatId, "🔙 Главное меню", Keyboards.MainMenu, ct);
                return;

            case Callbacks.Noop:
                // Кнопка-счётчик страниц: просто гашу «часики».
                await _messenger.AnswerAsync(query, ct: ct);
                return;

            default:
                _logger.LogWarning("Неизвестный callback {CallbackData} от пользователя {UserId}", query.Data, userId);
                await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
                return;
        }
    }

    /// <summary>Номер страницы из callback_data. Мусор считаю первой страницей.</summary>
    private static int ParsePage(string? argument) =>
        int.TryParse(argument, out var page) && page >= 0 ? page : 0;
}

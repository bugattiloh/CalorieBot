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
    private readonly LimitScenario _limit;
    private readonly ProgressScenario _progress;
    private readonly ILogger<UpdateRouter> _logger;

    public UpdateRouter(
        BotMessenger messenger,
        IUserService users,
        IConversationStateStore states,
        MealScenario meal,
        FavoritesScenario favorites,
        LimitScenario limit,
        ProgressScenario progress,
        ILogger<UpdateRouter> logger)
    {
        _messenger = messenger;
        _users = users;
        _states = states;
        _meal = meal;
        _favorites = favorites;
        _limit = limit;
        _progress = progress;
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

            case Buttons.History:
                await _progress.ShowHistoryAsync(chatId, userId, ct);
                return true;

            // Подменю «Добавить прием пищи».
            case Buttons.FromFavorites:
                await _meal.ShowFavoritesAsync(chatId, userId, page: 0, editMessageId: null, ct);
                return true;

            case Buttons.NewProduct:
                await _meal.StartNewProductAsync(chatId, userId, ct);
                return true;

            // Подменю «Любимые продукты».
            case Buttons.AddFavorite:
                await _favorites.StartAddAsync(chatId, userId, ct);
                return true;

            case Buttons.MyProducts:
                await _favorites.ShowListAsync(chatId, userId, page: 0, editMessageId: null, ct);
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

            case Callbacks.PickFavorite:
                await _meal.HandleFavoritePickedAsync(query, argument, ct);
                return;

            case Callbacks.PickFavoritePage:
                await _messenger.AnswerAsync(query, ct: ct);
                await _meal.ShowFavoritesAsync(chatId, userId, ParsePage(argument), messageId, ct);
                return;

            case Callbacks.ListPage:
                await _messenger.AnswerAsync(query, ct: ct);
                await _favorites.ShowListAsync(chatId, userId, ParsePage(argument), messageId, ct);
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
                    await _favorites.StartAddAsync(chatId, userId, ct);
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

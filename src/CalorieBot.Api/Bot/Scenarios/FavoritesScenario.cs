using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Validation;
using Telegram.Bot.Types;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «⭐ Любимые продукты»: добавление, просмотр постранично и удаление.
/// </summary>
public sealed class FavoritesScenario
{
    private readonly BotMessenger _messenger;
    private readonly IFavoriteProductService _favorites;
    private readonly IConversationStateStore _states;
    private readonly ILogger<FavoritesScenario> _logger;

    public FavoritesScenario(
        BotMessenger messenger,
        IFavoriteProductService favorites,
        IConversationStateStore states,
        ILogger<FavoritesScenario> logger)
    {
        _messenger = messenger;
        _favorites = favorites;
        _states = states;
        _logger = logger;
    }

    /// <summary>Показываю подменю избранного.</summary>
    public Task ShowMenuAsync(long chatId, long userId, CancellationToken ct)
    {
        _states.Get(userId).Reset();
        return _messenger.SendAsync(chatId, Texts.FavoritesMenuText, Keyboards.FavoritesMenu, ct);
    }

    /// <summary>Начинаю добавление продукта в избранное: название → БЖУ → порция.</summary>
    public async Task StartAddAsync(long chatId, long userId, CancellationToken ct)
    {
        var context = _states.Get(userId);
        context.Reset();
        context.State = ConversationState.AwaitingFavoriteName;

        await _messenger.SendAsync(chatId, Texts.AskProductName, Keyboards.FavoritesMenu, ct);
    }

    /// <summary>Принимаю название будущего избранного продукта и спрашиваю, как удобнее ввести БЖУ.</summary>
    public async Task HandleNameAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseProductName(text, out var name, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        context.ProductName = name;
        context.State = ConversationState.AwaitingFavoriteMacrosMode;

        var sent = await _messenger.SendAsync(chatId, Texts.AskMacrosMode, Keyboards.MacrosModeChoice(Callbacks.FavoriteMacrosMode), ct);
        context.ActiveInlineMessageId = sent.MessageId;
    }

    /// <summary>Пользователь выбрал, как вводить БЖУ — на 100 г или сразу на всю порцию.</summary>
    public async Task HandleMacrosModeAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var context = _states.Get(userId);

        if (string.IsNullOrEmpty(context.ProductName))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        context.MacrosPerHundredGrams = argument == "100";
        context.State = ConversationState.AwaitingFavoriteMacros;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            context.MacrosPerHundredGrams ? Texts.AskMacrosPerHundred : Texts.AskMacros,
            replyMarkup: null,
            ct);
    }

    /// <summary>
    /// Принимаю БЖУ. На порцию целиком — спрашиваю ещё размер порции текстом (шаг необязательный).
    /// На 100 г — запоминаю значения и жду вес порции: он же станет размером порции продукта.
    /// </summary>
    public async Task HandleMacrosAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseMacros(text, out var proteins, out var fats, out var carbs, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);

        if (string.IsNullOrWhiteSpace(context.ProductName))
        {
            // Диалог протух и название не сохранилось — прошу его заново, пустых продуктов в базе мне не нужно.
            await StartAddAsync(chatId, userId, ct);
            return;
        }

        if (context.MacrosPerHundredGrams)
        {
            context.Proteins = proteins;
            context.Fats = fats;
            context.Carbs = carbs;
            context.State = ConversationState.AwaitingFavoriteServingGrams;

            await _messenger.SendAsync(chatId, Texts.AskServingGrams, Keyboards.FavoritesMenu, ct);
            return;
        }

        context.Apply(ProductDraft.FromMacros(context.ProductName, proteins, fats, carbs));
        context.State = ConversationState.AwaitingFavoriteServingSize;

        var sent = await _messenger.SendAsync(
            chatId,
            $"{Texts.ProductCard(context.ToDraft())}\n\n{Texts.AskServingSize}",
            Keyboards.SkipServingSize,
            ct);

        context.ActiveInlineMessageId = sent.MessageId;
    }

    /// <summary>
    /// Принимаю вес порции, пересчитываю БЖУ со 100 г на неё и сохраняю — размер порции уже известен
    /// (это и есть введённый вес), поэтому отдельно спрашивать его не нужно.
    /// </summary>
    public async Task HandleServingGramsAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseServingGrams(text, out var grams, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);

        if (string.IsNullOrWhiteSpace(context.ProductName))
        {
            await StartAddAsync(chatId, userId, ct);
            return;
        }

        var scale = grams / 100m;
        var draft = ProductDraft.FromMacros(
            context.ProductName,
            Math.Round(context.Proteins * scale, 1),
            Math.Round(context.Fats * scale, 1),
            Math.Round(context.Carbs * scale, 1),
            servingSize: $"{grams} г");

        context.Apply(draft);

        await SaveAsync(chatId, userId, context, editMessageId: null, ct);
    }

    /// <summary>Принимаю описание порции текстом и сохраняю продукт.</summary>
    public async Task HandleServingSizeAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseServingSize(text, out var servingSize, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        context.ServingSize = servingSize;

        await SaveAsync(chatId, userId, context, editMessageId: null, ct);
    }

    /// <summary>Пользователь пропустил шаг с порцией — сохраняю продукт как есть.</summary>
    public async Task HandleSkipServingSizeAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var context = _states.Get(userId);

        if (string.IsNullOrEmpty(context.ProductName))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        context.ServingSize = null;

        await _messenger.AnswerAsync(query, ct: ct);
        await SaveAsync(query.Message.Chat.Id, userId, context, query.Message.MessageId, ct);
    }

    /// <summary>Показываю страницу списка «Мои продукты».</summary>
    public async Task ShowListAsync(long chatId, long userId, int page, int? editMessageId, CancellationToken ct)
    {
        var favorites = await _favorites.GetAllAsync(userId, ct);

        if (favorites.Count == 0)
        {
            await SendOrEditAsync(chatId, editMessageId, Texts.EmptyFavorites, Keyboards.ToMenuOnly, ct);
            return;
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(favorites.Count / (double)Keyboards.PageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        await SendOrEditAsync(
            chatId,
            editMessageId,
            Texts.FavoritesPage(favorites, page, Keyboards.PageSize),
            Keyboards.PageNavigation(page, totalPages, Callbacks.ListPage),
            ct);
    }

    /// <summary>Показываю страницу списка удаления.</summary>
    public async Task ShowDeleteListAsync(long chatId, long userId, int page, int? editMessageId, CancellationToken ct)
    {
        var favorites = await _favorites.GetAllAsync(userId, ct);

        if (favorites.Count == 0)
        {
            await SendOrEditAsync(chatId, editMessageId, Texts.EmptyFavorites, Keyboards.ToMenuOnly, ct);
            return;
        }

        var keyboard = Keyboards.ProductPage(
            favorites,
            page,
            Callbacks.DeleteFavorite,
            Callbacks.DeletePage,
            product => Texts.ProductButtonLabel(product, fitsIntoLimit: true));

        await SendOrEditAsync(chatId, editMessageId, Texts.DeleteListHeader(favorites.Count), keyboard, ct);
    }

    /// <summary>Спрашиваю подтверждение перед удалением — случайный тап не должен стирать продукт.</summary>
    public async Task HandleDeleteRequestAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var favoriteId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var product = await _favorites.GetAsync(query.From.Id, favoriteId, ct);

        if (product is null)
        {
            await _messenger.AnswerAsync(query, "Продукт уже удалён.", showAlert: true, ct: ct);
            await ShowDeleteListAsync(query.Message.Chat.Id, query.From.Id, 0, query.Message.MessageId, ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.ConfirmDelete(product),
            Keyboards.DeleteConfirm(product.Id),
            ct);
    }

    /// <summary>Удаляю продукт и возвращаю пользователя к списку.</summary>
    public async Task HandleDeleteConfirmAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var favoriteId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var deleted = await _favorites.DeleteAsync(userId, favoriteId, ct);

        if (deleted is null)
        {
            await _messenger.AnswerAsync(query, "Продукт уже удалён.", ct: ct);
        }
        else
        {
            await _messenger.AnswerAsync(query, "Удалил 🗑", ct: ct);
        }

        var favorites = await _favorites.GetAllAsync(userId, ct);
        var header = deleted is null ? Texts.StaleDialog : Texts.FavoriteDeleted(deleted.Name);

        if (favorites.Count == 0)
        {
            await _messenger.EditAsync(
                query.Message.Chat.Id,
                query.Message.MessageId,
                $"{header}\n\n{Texts.EmptyFavorites}",
                Keyboards.ToMenuOnly,
                ct);
            return;
        }

        var keyboard = Keyboards.ProductPage(
            favorites,
            0,
            Callbacks.DeleteFavorite,
            Callbacks.DeletePage,
            product => Texts.ProductButtonLabel(product, fitsIntoLimit: true));

        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            $"{header}\n\n{Texts.DeleteListHeader(favorites.Count)}",
            keyboard,
            ct);
    }

    /// <summary>Общая точка сохранения: и для введённой порции, и для пропущенного шага.</summary>
    private async Task SaveAsync(
        long chatId,
        long userId,
        ConversationContext context,
        int? editMessageId,
        CancellationToken ct)
    {
        var draft = context.ToDraft();
        var (created, product) = await _favorites.AddOrUpdateAsync(userId, draft, ct);

        context.Reset();

        await SendOrEditAsync(
            chatId,
            editMessageId,
            Texts.FavoriteSaved(product, created),
            Keyboards.AfterFavoriteSaved,
            ct);

        _logger.LogInformation(
            "Пользователь {UserId} сохранил в избранное «{ProductName}» ({Calories} ккал)",
            userId, product.Name, product.Calories);
    }

    private async Task SendOrEditAsync(
        long chatId,
        int? editMessageId,
        string text,
        Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup keyboard,
        CancellationToken ct)
    {
        if (editMessageId is null)
        {
            await _messenger.SendAsync(chatId, text, keyboard, ct);
        }
        else
        {
            await _messenger.EditAsync(chatId, editMessageId.Value, text, keyboard, ct);
        }
    }
}

using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Validation;
using CalorieBot.Data.Entities;
using Telegram.Bot.Types;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «🍽 Добавить прием пищи»: выбор из избранного либо ввод нового продукта,
/// затем предложение сохранить продукт в избранное и выбор типа приёма пищи.
/// </summary>
public sealed class MealScenario
{
    private readonly BotMessenger _messenger;
    private readonly IFavoriteProductService _favorites;
    private readonly IFoodLogService _foodLog;
    private readonly IProgressService _progress;
    private readonly IConversationStateStore _states;
    private readonly ILogger<MealScenario> _logger;

    public MealScenario(
        BotMessenger messenger,
        IFavoriteProductService favorites,
        IFoodLogService foodLog,
        IProgressService progress,
        IConversationStateStore states,
        ILogger<MealScenario> logger)
    {
        _messenger = messenger;
        _favorites = favorites;
        _foodLog = foodLog;
        _progress = progress;
        _states = states;
        _logger = logger;
    }

    /// <summary>Показываю подменю выбора способа добавления.</summary>
    public Task ShowMenuAsync(long chatId, long userId, CancellationToken ct)
    {
        _states.Get(userId).Reset();
        return _messenger.SendAsync(chatId, Texts.ChooseMealSource, Keyboards.MealMenu, ct);
    }

    /// <summary>
    /// Показываю страницу избранного для записи в дневник.
    /// Если <paramref name="editMessageId"/> задан — не плодю сообщения, а переписываю существующее.
    /// </summary>
    public async Task ShowFavoritesAsync(
        long chatId,
        long userId,
        int page,
        int? editMessageId,
        CancellationToken ct)
    {
        var favorites = await _favorites.GetAllAsync(userId, ct);

        if (favorites.Count == 0)
        {
            if (editMessageId is null)
            {
                await _messenger.SendAsync(chatId, Texts.EmptyFavorites, Keyboards.MealMenu, ct);
            }
            else
            {
                await _messenger.EditAsync(chatId, editMessageId.Value, Texts.EmptyFavorites, Keyboards.ToMenuOnly, ct);
            }

            return;
        }

        var progress = await _progress.GetCurrentCycleAsync(userId, ct);
        var text = Texts.PickFavoriteHeader(progress, favorites.Count);

        // Продукты, которые уже не влезают в остаток лимита, помечаю — но выбрать их не запрещаю.
        var keyboard = Keyboards.ProductPage(
            favorites,
            page,
            Callbacks.PickFavorite,
            Callbacks.PickFavoritePage,
            product => Texts.ProductButtonLabel(product, product.Calories <= progress.RemainingCalories));

        var context = _states.Get(userId);

        if (editMessageId is null)
        {
            var sent = await _messenger.SendAsync(chatId, text, keyboard, ct);
            context.ActiveInlineMessageId = sent.MessageId;
        }
        else
        {
            await _messenger.EditAsync(chatId, editMessageId.Value, text, keyboard, ct);
            context.ActiveInlineMessageId = editMessageId;
        }
    }

    /// <summary>Пользователь выбрал продукт из избранного — остаётся указать тип приёма пищи.</summary>
    public async Task HandleFavoritePickedAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var favoriteId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var favorite = await _favorites.GetAsync(userId, favoriteId, ct);

        if (favorite is null)
        {
            await _messenger.AnswerAsync(query, "Продукт не найден — возможно, он уже удалён.", showAlert: true, ct: ct);
            return;
        }

        var context = _states.Get(userId);
        context.Reset();
        context.Apply(ProductDraft.FromFavorite(favorite));
        context.FavoriteProductId = favorite.Id;
        context.ActiveInlineMessageId = query.Message.MessageId;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.ChooseMealType(context.ToDraft()),
            Keyboards.MealTypes,
            ct);
    }

    /// <summary>Начинаю ввод нового продукта: сначала название.</summary>
    public async Task StartNewProductAsync(long chatId, long userId, CancellationToken ct)
    {
        var context = _states.Get(userId);
        context.Reset();
        context.State = ConversationState.AwaitingMealProductName;

        await _messenger.SendAsync(chatId, Texts.AskProductName, Keyboards.MealMenu, ct);
    }

    /// <summary>Принимаю название нового продукта и спрашиваю, как удобнее ввести БЖУ.</summary>
    public async Task HandleNameAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseProductName(text, out var name, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.MealMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        context.ProductName = name;
        context.State = ConversationState.AwaitingMealMacrosMode;

        var sent = await _messenger.SendAsync(chatId, Texts.AskMacrosMode, Keyboards.MacrosModeChoice(Callbacks.MealMacrosMode), ct);
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
        context.State = ConversationState.AwaitingMealProductMacros;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            context.MacrosPerHundredGrams ? Texts.AskMacrosPerHundred : Texts.AskMacros,
            replyMarkup: null,
            ct);
    }

    /// <summary>
    /// Принимаю БЖУ. На порцию целиком — сразу считаю калории и предлагаю сохранить в избранное.
    /// На 100 г — запоминаю значения и жду вес порции, чтобы пересчитать их на реальную порцию.
    /// </summary>
    public async Task HandleMacrosAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseMacros(text, out var proteins, out var fats, out var carbs, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.MealMenu, ct);
            return;
        }

        var context = _states.Get(userId);

        if (string.IsNullOrWhiteSpace(context.ProductName))
        {
            // Название потерялось (например, диалог провисел дольше часа) — начинаю шаг заново.
            await StartNewProductAsync(chatId, userId, ct);
            return;
        }

        if (context.MacrosPerHundredGrams)
        {
            context.Proteins = proteins;
            context.Fats = fats;
            context.Carbs = carbs;
            context.State = ConversationState.AwaitingMealServingGrams;

            await _messenger.SendAsync(chatId, Texts.AskServingGrams, Keyboards.MealMenu, ct);
            return;
        }

        var draft = ProductDraft.FromMacros(context.ProductName, proteins, fats, carbs);
        await OfferToSaveFavoriteAsync(chatId, context, draft, ct);
    }

    /// <summary>Принимаю вес порции и пересчитываю запомненные БЖУ со 100 г на реальную порцию.</summary>
    public async Task HandleServingGramsAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseServingGrams(text, out var grams, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.MealMenu, ct);
            return;
        }

        var context = _states.Get(userId);

        if (string.IsNullOrWhiteSpace(context.ProductName))
        {
            await StartNewProductAsync(chatId, userId, ct);
            return;
        }

        // В context.Proteins/Fats/Carbs сейчас лежат значения на 100 г — масштабирую на введённый вес.
        var scale = grams / 100m;
        var draft = ProductDraft.FromMacros(
            context.ProductName,
            Math.Round(context.Proteins * scale, 1),
            Math.Round(context.Fats * scale, 1),
            Math.Round(context.Carbs * scale, 1),
            servingSize: $"{grams} г");

        await OfferToSaveFavoriteAsync(chatId, context, draft, ct);
    }

    /// <summary>Общая точка после того, как черновик продукта готов: показываю карточку и предлагаю сохранить в избранное.</summary>
    private async Task OfferToSaveFavoriteAsync(long chatId, ConversationContext context, ProductDraft draft, CancellationToken ct)
    {
        context.Apply(draft);

        // Дальше только инлайн-кнопки, текстовый ввод больше не жду.
        context.State = ConversationState.Idle;

        var sent = await _messenger.SendAsync(
            chatId,
            Texts.OfferToSaveFavorite(draft),
            Keyboards.SaveFavoriteConfirm,
            ct);

        context.ActiveInlineMessageId = sent.MessageId;
    }

    /// <summary>Обрабатываю ответ на вопрос «сохранить в избранное?» и перехожу к выбору приёма пищи.</summary>
    public async Task HandleSaveFavoriteAnswerAsync(CallbackQuery query, bool save, CancellationToken ct)
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

        var answer = "Продукт не сохраняю";

        if (save)
        {
            var (created, product) = await _favorites.AddOrUpdateAsync(userId, context.ToDraft(), ct);
            context.FavoriteProductId = product.Id;
            answer = created ? "Добавил в избранное ⭐" : "Обновил продукт в избранном ⭐";
        }

        await _messenger.AnswerAsync(query, answer, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.ChooseMealType(context.ToDraft()),
            Keyboards.MealTypes,
            ct);
    }

    /// <summary>Финальный шаг: записываю продукт в дневник и показываю обновлённый прогресс.</summary>
    public async Task HandleMealTypeAsync(CallbackQuery query, string? argument, CancellationToken ct)
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

        if (!int.TryParse(argument, out var rawMealType) || !Enum.IsDefined(typeof(MealType), rawMealType))
        {
            await _messenger.AnswerAsync(query, "Не понял тип приёма пищи.", showAlert: true, ct: ct);
            return;
        }

        var entry = await _foodLog.LogAsync(
            userId,
            context.ToDraft(),
            (MealType)rawMealType,
            context.FavoriteProductId,
            ct);

        var progress = await _progress.GetCurrentCycleAsync(userId, ct);

        // Сценарий завершён — освобождаю состояние, чтобы старые кнопки ничего не записали повторно.
        context.Reset();

        await _messenger.AnswerAsync(query, "Записал ✅", ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.MealLogged(entry, progress),
            Keyboards.AfterMealLogged,
            ct);

        if (progress.IsExceeded)
        {
            _logger.LogInformation(
                "Пользователь {UserId} превысил дневной лимит: {Consumed} из {Limit} ккал",
                userId, progress.ConsumedCalories, progress.CalorieLimit);
        }
    }
}

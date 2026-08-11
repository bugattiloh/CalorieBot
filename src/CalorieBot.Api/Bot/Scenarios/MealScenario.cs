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

        // Продукты, которые уже не влезают в остаток, помечаю — но выбрать их не запрещаю.
        var keyboard = Keyboards.ProductPage(
            favorites,
            page,
            Callbacks.PickFavorite,
            Callbacks.PickFavoritePage,
            product => Texts.ProductButtonLabel(product, progress.Fits(product)));

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

    /// <summary>
    /// Пользователь выбрал продукт из избранного. С фиксированной порцией — остаётся указать тип приёма пищи.
    /// С плавающей порцией (например, рис) — сначала спрашиваю, сколько съедено, чтобы пересчитать КБЖУ.
    /// </summary>
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
        context.FavoriteProductId = favorite.Id;
        context.ActiveInlineMessageId = query.Message.MessageId;

        if (!favorite.IsFixedServing)
        {
            // КБЖУ у такого избранного хранятся на 100 г (или на 1 л для «Воды») — прежде чем считать,
            // узнаю фактический объём/вес порции.
            context.ProductName = favorite.Name;
            context.Proteins = favorite.Proteins;
            context.Fats = favorite.Fats;
            context.Carbs = favorite.Carbs;
            context.MacrosPerHundredGrams = true;
            context.IsLiterServing = favorite.CategoryKind == FavoriteCategoryKind.Water;
            context.State = ConversationState.AwaitingMealServingGrams;

            var prompt = context.IsLiterServing ? Texts.AskServingLiters : Texts.AskServingGrams;
            await _messenger.AnswerAsync(query, ct: ct);
            await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, prompt, replyMarkup: null, ct);
            return;
        }

        context.Apply(ProductDraft.FromFavorite(favorite));

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

    /// <summary>Принимаю вес/объём порции и пересчитываю запомненные БЖУ (со 100 г или с 1 л) на реальную порцию.</summary>
    public async Task HandleServingGramsAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        var context = _states.Get(userId);

        decimal scale;
        string servingSizeText;

        if (context.IsLiterServing)
        {
            if (!InputParser.TryParseLiters(text, out var liters, out var literError))
            {
                await _messenger.SendAsync(chatId, Texts.ValidationError(literError), Keyboards.MealMenu, ct);
                return;
            }

            scale = liters;
            servingSizeText = $"{Texts.Num(liters)} л";
        }
        else
        {
            if (!InputParser.TryParseServingGrams(text, out var grams, out var error))
            {
                await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.MealMenu, ct);
                return;
            }

            // В context.Proteins/Fats/Carbs сейчас лежат значения на 100 г — масштабирую на введённый вес.
            scale = grams / 100m;
            servingSizeText = $"{grams} г";
        }

        if (string.IsNullOrWhiteSpace(context.ProductName))
        {
            await StartNewProductAsync(chatId, userId, ct);
            return;
        }

        var draft = ProductDraft.FromMacros(
            context.ProductName,
            Math.Round(context.Proteins * scale, 1),
            Math.Round(context.Fats * scale, 1),
            Math.Round(context.Carbs * scale, 1),
            servingSize: servingSizeText);

        if (context.FavoriteProductId.HasValue)
        {
            // Вес спрашивал для уже существующего избранного — сохранять его повторно не нужно,
            // сразу перехожу к выбору приёма пищи.
            context.Apply(draft);
            context.State = ConversationState.Idle;

            var sent = await _messenger.SendAsync(chatId, Texts.ChooseMealType(draft), Keyboards.MealTypes, ct);
            context.ActiveInlineMessageId = sent.MessageId;
            return;
        }

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
            // Продукт уже съеден именно в этом количестве — сохраняю как фиксированную порцию.
            var (created, product) = await _favorites.AddOrUpdateAsync(userId, context.ToDraft(), isFixedServing: true, ct);
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

    /// <summary>
    /// Пользователь выбрал тип приёма пищи. Если продукт с таким же названием уже записан в этом цикле —
    /// сперва спрашиваю, заменить старую запись или добавить новую отдельно.
    /// </summary>
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

        var duplicate = await FindDuplicateInCurrentCycleAsync(userId, context.ProductName, ct);
        if (duplicate is not null)
        {
            await _messenger.AnswerAsync(query, ct: ct);
            await _messenger.EditAsync(
                query.Message.Chat.Id,
                query.Message.MessageId,
                Texts.AskDuplicateMealConfirm(duplicate, context.ToDraft()),
                Keyboards.DuplicateMealConfirm(rawMealType, duplicate.Id),
                ct);
            return;
        }

        await LogMealAndShowResultAsync(query, userId, context, (MealType)rawMealType, ct);
    }

    /// <summary>Ответ на предупреждение о повторном названии: заменить прошлую запись или добавить рядом с ней.</summary>
    public async Task HandleDuplicateMealConfirmAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var context = _states.Get(userId);

        var parts = (argument ?? string.Empty).Split(':');
        if (parts.Length != 3
            || !int.TryParse(parts[1], out var rawMealType) || !Enum.IsDefined(typeof(MealType), rawMealType)
            || !int.TryParse(parts[2], out var entryId)
            || string.IsNullOrEmpty(context.ProductName))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        if (parts[0] == "yes")
        {
            await _foodLog.DeleteAsync(userId, entryId, ct);
        }

        await LogMealAndShowResultAsync(query, userId, context, (MealType)rawMealType, ct);
    }

    /// <summary>Ищу в текущем цикле последнюю запись с тем же названием продукта (без учёта регистра).</summary>
    private async Task<FoodLogEntry?> FindDuplicateInCurrentCycleAsync(long userId, string productName, CancellationToken ct)
    {
        var entries = await _foodLog.GetCurrentCycleAsync(userId, ct);
        return entries
            .Where(e => string.Equals(e.ProductName, productName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.LoggedAt)
            .FirstOrDefault();
    }

    /// <summary>Финальный шаг: записываю продукт в дневник и показываю обновлённый прогресс.</summary>
    private async Task LogMealAndShowResultAsync(
        CallbackQuery query, long userId, ConversationContext context, MealType mealType, CancellationToken ct)
    {
        var entry = await _foodLog.LogAsync(
            userId,
            context.ToDraft(),
            mealType,
            context.FavoriteProductId,
            ct);

        var progress = await _progress.GetCurrentCycleAsync(userId, ct);

        // Сценарий завершён — освобождаю состояние, чтобы старые кнопки ничего не записали повторно.
        context.Reset();

        await _messenger.AnswerAsync(query, "Записал ✅", ct: ct);
        await _messenger.EditAsync(
            query.Message!.Chat.Id,
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

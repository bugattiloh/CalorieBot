using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Validation;
using CalorieBot.Data.Entities;
using Telegram.Bot.Types;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «🍲 Готовые блюда»: блюдо — это избранное, чьё КБЖУ считается автосуммой ингредиентов,
/// а не вводится вручную. Состав можно менять — добавлять и убирать ингредиенты по одному.
/// </summary>
public sealed class DishScenario
{
    private readonly BotMessenger _messenger;
    private readonly IFavoriteProductService _favorites;
    private readonly IConversationStateStore _states;
    private readonly ILogger<DishScenario> _logger;

    public DishScenario(
        BotMessenger messenger,
        IFavoriteProductService favorites,
        IConversationStateStore states,
        ILogger<DishScenario> logger)
    {
        _messenger = messenger;
        _favorites = favorites;
        _states = states;
        _logger = logger;
    }

    /// <summary>Показываю список готовых блюд.</summary>
    public async Task ShowListAsync(long chatId, long userId, int page, int? editMessageId, CancellationToken ct)
    {
        var dishes = await _favorites.GetByCategoryAsync(userId, FavoriteCategoryKind.Dish, null, ct);

        var keyboard = Keyboards.CategoryItemList(
            dishes,
            page,
            Callbacks.DishDetails,
            Callbacks.DishPage,
            Callbacks.DishAdd,
            "➕ Создать блюдо",
            "◀️ Избранное",
            Callbacks.FavoritesMenu,
            product => Texts.ProductButtonLabel(product, fitsIntoLimit: true));

        await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.DishListHeader(dishes.Count), keyboard, ct);
    }

    /// <summary>Начинаю создание нового блюда — сперва только название, ингредиенты добавляются потом.</summary>
    public async Task StartCreateAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        context.Reset();
        context.State = ConversationState.AwaitingDishName;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskDishName, replyMarkup: null, ct);
    }

    /// <summary>Принимаю название блюда, сразу завожу его (КБЖУ 0, наполняется ингредиентами) и открываю карточку.</summary>
    public async Task HandleNameAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseProductName(text, out var name, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        _states.Get(userId).Reset();

        var dish = await _favorites.CreateDishAsync(userId, name, ct);
        var keyboard = Keyboards.DishDetailsActions(dish.Id, Array.Empty<DishIngredient>(), Texts.IngredientButtonLabel);

        await _messenger.SendAsync(chatId, Texts.DishCard(dish), keyboard, ct);
    }

    /// <summary>Открываю карточку блюда по тапу из списка.</summary>
    public async Task ShowDetailsAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var dishId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await ShowDetailsInternalAsync(query.Message.Chat.Id, query.From.Id, dishId, query.Message.MessageId, ct);
    }

    /// <summary>По кнопке «➕ Добавить ингредиент» сперва спрашиваю источник: из избранного или свой продукт.</summary>
    public async Task StartAddIngredientAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var dishId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id, query.Message.MessageId, Texts.AskIngredientSource, Keyboards.DishIngredientSourceChoice(dishId), ct);
    }

    /// <summary>Начинаю добавление ингредиента вручную: название → БЖУ на весь добавленный ингредиент.</summary>
    public async Task StartAddCustomIngredientAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var dishId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        context.Reset();
        context.EditingDishId = dishId;
        context.State = ConversationState.AwaitingDishIngredientName;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskIngredientName, replyMarkup: null, ct);
    }

    /// <summary>Показываю страницу избранных продуктов, чтобы выбрать один как ингредиент.</summary>
    public async Task ShowFavoriteIngredientPickerAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null || parts.Length == 0 || !int.TryParse(parts[0], out var dishId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var page = parts.Length > 1 && int.TryParse(parts[1], out var parsedPage) ? parsedPage : 0;
        var userId = query.From.Id;

        // Ингредиентом может стать только обычный продукт — не вода и не другое блюдо.
        var products = (await _favorites.GetAllAsync(userId, ct))
            .Where(p => p.CategoryKind == FavoriteCategoryKind.Product)
            .OrderBy(p => p.Name)
            .ToList();

        await _messenger.AnswerAsync(query, ct: ct);

        if (products.Count == 0)
        {
            await _messenger.EditAsync(
                query.Message.Chat.Id,
                query.Message.MessageId,
                "В «Продуктах» пока пусто — сначала добавьте что-нибудь в ⭐ Избранное → 🥘 Продукты, либо введите ингредиент вручную.",
                Keyboards.DishIngredientSourceChoice(dishId),
                ct);
            return;
        }

        var keyboard = Keyboards.DishIngredientFavoritePicker(products, page, dishId, p => Texts.ProductButtonLabel(p, fitsIntoLimit: true));
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.PickIngredientFromFavoritesHeader(products.Count), keyboard, ct);
    }

    /// <summary>
    /// Пользователь выбрал избранный продукт как ингредиент. Фиксированная порция добавляется как есть,
    /// плавающая (на 100 г) — сперва спрашиваю вес, как и при обычном логировании еды.
    /// </summary>
    public async Task HandlePickFavoriteIngredientAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null || parts.Length != 2 || !int.TryParse(parts[0], out var dishId) || !int.TryParse(parts[1], out var favoriteId))
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

        if (favorite.IsFixedServing)
        {
            await _messenger.AnswerAsync(query, "Добавил ✅", ct: ct);
            var dish = await _favorites.AddDishIngredientAsync(userId, dishId, ProductDraft.FromFavorite(favorite), ct);
            await ShowUpdatedDishOrNotFoundAsync(query.Message.Chat.Id, userId, dishId, query.Message.MessageId, dish, ct);
            return;
        }

        // Плавающая порция (на 100 г) — прежде чем добавить, узнаю фактический вес.
        var context = _states.Get(userId);
        context.Reset();
        context.EditingDishId = dishId;
        context.ProductName = favorite.Name;
        context.Proteins = favorite.Proteins;
        context.Fats = favorite.Fats;
        context.Carbs = favorite.Carbs;
        context.State = ConversationState.AwaitingDishIngredientGrams;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskServingGrams, replyMarkup: null, ct);
    }

    /// <summary>Принимаю вес плавающей порции избранного ингредиента, пересчитываю БЖУ и добавляю его в блюдо.</summary>
    public async Task HandleIngredientGramsAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        var context = _states.Get(userId);

        if (context.EditingDishId is not { } dishId || string.IsNullOrWhiteSpace(context.ProductName))
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.FavoritesMenu, ct);
            return;
        }

        if (!InputParser.TryParseServingGrams(text, out var grams, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var scale = grams / 100m;
        var draft = ProductDraft.FromMacros(
            context.ProductName,
            Math.Round(context.Proteins * scale, 1),
            Math.Round(context.Fats * scale, 1),
            Math.Round(context.Carbs * scale, 1),
            servingSize: $"{grams} г");

        context.Reset();

        var dish = await _favorites.AddDishIngredientAsync(userId, dishId, draft, ct);
        await ShowUpdatedDishOrNotFoundAsync(chatId, userId, dishId, editMessageId: null, dish, ct);
    }

    /// <summary>Принимаю название ингредиента и спрашиваю его БЖУ.</summary>
    public async Task HandleIngredientNameAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        var context = _states.Get(userId);

        if (context.EditingDishId is null)
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.FavoritesMenu, ct);
            return;
        }

        if (!InputParser.TryParseProductName(text, out var name, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        context.ProductName = name;
        context.State = ConversationState.AwaitingDishIngredientMacros;

        await _messenger.SendAsync(chatId, Texts.AskMacros, Keyboards.FavoritesMenu, ct);
    }

    /// <summary>Принимаю БЖУ ингредиента, добавляю его к блюду — сервис сам пересчитывает автосумму.</summary>
    public async Task HandleIngredientMacrosAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        var context = _states.Get(userId);

        if (context.EditingDishId is not { } dishId || string.IsNullOrWhiteSpace(context.ProductName))
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.FavoritesMenu, ct);
            return;
        }

        if (!InputParser.TryParseMacros(text, out var proteins, out var fats, out var carbs, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var draft = ProductDraft.FromMacros(context.ProductName, proteins, fats, carbs);
        context.Reset();

        var dish = await _favorites.AddDishIngredientAsync(userId, dishId, draft, ct);
        await ShowUpdatedDishOrNotFoundAsync(chatId, userId, dishId, editMessageId: null, dish, ct);

        _logger.LogInformation("Пользователь {UserId} добавил ингредиент «{Name}» в блюдо {DishId}", userId, draft.Name, dishId);
    }

    /// <summary>Спрашиваю подтверждение перед удалением ингредиента.</summary>
    public async Task HandleIngredientDeleteRequestAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null || parts.Length != 2 || !int.TryParse(parts[0], out var dishId) || !int.TryParse(parts[1], out var ingredientId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var ingredients = await _favorites.GetDishIngredientsAsync(userId, dishId, ct);
        var ingredient = ingredients.FirstOrDefault(i => i.Id == ingredientId);

        if (ingredient is null)
        {
            await _messenger.AnswerAsync(query, "Ингредиент не найден — возможно, уже удалён.", showAlert: true, ct: ct);
            await ShowDetailsInternalAsync(query.Message.Chat.Id, userId, dishId, query.Message.MessageId, ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.ConfirmDeleteIngredient(ingredient),
            Keyboards.DishIngredientDeleteConfirm(dishId, ingredientId),
            ct);
    }

    /// <summary>Убираю ингредиент (сервис пересчитывает автосумму) и показываю обновлённую карточку.</summary>
    public async Task HandleIngredientDeleteConfirmAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null || parts.Length != 2 || !int.TryParse(parts[0], out var dishId) || !int.TryParse(parts[1], out var ingredientId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        await _favorites.RemoveDishIngredientAsync(userId, dishId, ingredientId, ct);

        await _messenger.AnswerAsync(query, "Убрал 🗑", ct: ct);
        await ShowDetailsInternalAsync(query.Message.Chat.Id, userId, dishId, query.Message.MessageId, ct);
    }

    private async Task ShowDetailsInternalAsync(long chatId, long userId, int dishId, int? editMessageId, CancellationToken ct)
    {
        var dish = await _favorites.GetAsync(userId, dishId, ct);
        if (dish is null || dish.CategoryKind != FavoriteCategoryKind.Dish)
        {
            await _messenger.SendOrEditAsync(chatId, editMessageId, "Блюдо не найдено — возможно, уже удалено.", Keyboards.ToMenuOnly, ct);
            return;
        }

        var ingredients = await _favorites.GetDishIngredientsAsync(userId, dishId, ct);
        var keyboard = Keyboards.DishDetailsActions(dishId, ingredients, Texts.IngredientButtonLabel);

        await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.DishCard(dish), keyboard, ct);
    }

    /// <summary>Общая точка после добавления ингредиента (свой или из избранного) — карточка блюда либо «не найдено».</summary>
    private async Task ShowUpdatedDishOrNotFoundAsync(
        long chatId, long userId, int dishId, int? editMessageId, FavoriteProduct? dish, CancellationToken ct)
    {
        if (dish is null)
        {
            await _messenger.SendOrEditAsync(chatId, editMessageId, "Блюдо не найдено — возможно, уже удалено.", Keyboards.ToMenuOnly, ct);
            return;
        }

        var ingredients = await _favorites.GetDishIngredientsAsync(userId, dishId, ct);
        var keyboard = Keyboards.DishDetailsActions(dishId, ingredients, Texts.IngredientButtonLabel);

        await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.DishCard(dish), keyboard, ct);
    }
}

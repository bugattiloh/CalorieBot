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

    /// <summary>Начинаю добавление ингредиента: название → БЖУ на весь добавленный ингредиент.</summary>
    public async Task StartAddIngredientAsync(CallbackQuery query, string? argument, CancellationToken ct)
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
        if (dish is null)
        {
            await _messenger.SendAsync(chatId, "Блюдо не найдено — возможно, уже удалено.", Keyboards.FavoritesMenu, ct);
            return;
        }

        var ingredients = await _favorites.GetDishIngredientsAsync(userId, dishId, ct);
        var keyboard = Keyboards.DishDetailsActions(dishId, ingredients, Texts.IngredientButtonLabel);

        await _messenger.SendAsync(chatId, Texts.DishCard(dish), keyboard, ct);

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
}

using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Validation;
using CalorieBot.Data.Entities;
using Telegram.Bot.Types;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «⭐ Избранное»: три группы — «Вода», «Готовые блюда» (см. <see cref="DishScenario"/>) и «Продукты»
/// с пользовательскими подкатегориями. Список избранного может быть огромным, поэтому вместо одного
/// плоского списка везде — навигация по группам/подкатегориям.
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

    // ------------------------------------------------------------------
    // Вода
    // ------------------------------------------------------------------

    /// <summary>Показываю список «Вода» — при первом обращении сам завожу пустой элемент «Вода» 0/0/0/0.</summary>
    public async Task ShowWaterListAsync(long chatId, long userId, int page, int? editMessageId, CancellationToken ct)
    {
        await _favorites.EnsureWaterSeedAsync(userId, ct);
        var items = await _favorites.GetByCategoryAsync(userId, FavoriteCategoryKind.Water, null, ct);

        var keyboard = Keyboards.CategoryItemList(
            items,
            page,
            Callbacks.FavoriteDetails,
            Callbacks.WaterPage,
            Callbacks.WaterAdd,
            "➕ Добавить жидкость",
            product => Texts.ProductButtonLabel(product, fitsIntoLimit: true));

        await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.WaterListHeader(items.Count), keyboard, ct);
    }

    /// <summary>Начинаю добавление новой жидкости: название → БЖУ на 1 л (тип порции спрашивать не нужно — всегда плавающая).</summary>
    public async Task StartAddWaterAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        context.Reset();
        context.State = ConversationState.AwaitingWaterName;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskWaterName, replyMarkup: null, ct);
    }

    /// <summary>Принимаю название жидкости и спрашиваю БЖУ на литр.</summary>
    public async Task HandleWaterNameAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseProductName(text, out var name, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        context.ProductName = name;
        context.State = ConversationState.AwaitingWaterMacros;

        await _messenger.SendAsync(chatId, Texts.AskMacrosPerLiter, Keyboards.FavoritesMenu, ct);
    }

    /// <summary>Принимаю БЖУ на литр и сразу сохраняю — для «Воды» лишних шагов не нужно.</summary>
    public async Task HandleWaterMacrosAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseMacros(text, out var proteins, out var fats, out var carbs, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        if (string.IsNullOrWhiteSpace(context.ProductName))
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.FavoritesMenu, ct);
            return;
        }

        var draft = ProductDraft.FromMacros(context.ProductName, proteins, fats, carbs);
        var (created, product) = await _favorites.AddOrUpdateAsync(
            userId, draft, isFixedServing: false, ct, categoryKind: FavoriteCategoryKind.Water);

        context.Reset();

        await _messenger.SendAsync(chatId, Texts.FavoriteSaved(product, created), Keyboards.AfterFavoriteSaved, ct);
        _logger.LogInformation("Пользователь {UserId} сохранил в «Воду» «{Name}»", userId, product.Name);
    }

    // ------------------------------------------------------------------
    // Продукты — подкатегории
    // ------------------------------------------------------------------

    /// <summary>Показываю список подкатегорий «Продуктов» — на первое обращение сам заводит 4 базовые.</summary>
    public async Task ShowProductCategoriesAsync(long chatId, long userId, int? editMessageId, bool manageMode, CancellationToken ct)
    {
        var categories = await _favorites.GetProductCategoriesAsync(userId, ct);
        var uncategorized = await _favorites.GetByCategoryAsync(userId, FavoriteCategoryKind.Product, null, ct);

        var keyboard = Keyboards.ProductCategoriesList(categories, uncategorized.Count > 0, manageMode);
        await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.ProductCategoriesHeader(manageMode), keyboard, ct);
    }

    /// <summary>Пользователь выбрал подкатегорию для просмотра (0 — «без категории»); аргумент может нести и страницу: «{id}:{page}».</summary>
    public async Task HandleProductCategoryPickAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var parts = (argument ?? string.Empty).Split(':');
        if (parts.Length == 0 || !int.TryParse(parts[0], out var categoryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var page = parts.Length > 1 && int.TryParse(parts[1], out var parsedPage) ? parsedPage : 0;

        await _messenger.AnswerAsync(query, ct: ct);
        await ShowProductsInCategoryAsync(query.Message.Chat.Id, query.From.Id, categoryId, page, query.Message.MessageId, ct);
    }

    /// <summary>Показываю продукты внутри подкатегории (<paramref name="categoryId"/> = 0 — «без категории»).</summary>
    public async Task ShowProductsInCategoryAsync(
        long chatId, long userId, int categoryId, int page, int? editMessageId, CancellationToken ct)
    {
        int? realCategoryId = categoryId == 0 ? null : categoryId;
        var items = await _favorites.GetByCategoryAsync(userId, FavoriteCategoryKind.Product, realCategoryId, ct);

        string categoryName;
        if (realCategoryId is null)
        {
            categoryName = "Без категории";
        }
        else
        {
            var categories = await _favorites.GetProductCategoriesAsync(userId, ct);
            categoryName = categories.FirstOrDefault(c => c.Id == realCategoryId)?.Name ?? "Подкатегория";
        }

        var keyboard = Keyboards.CategoryItemList(
            items,
            page,
            Callbacks.FavoriteDetails,
            Callbacks.Build(Callbacks.ProductCategoryPick, categoryId),
            Callbacks.Build(Callbacks.ProductAddHere, categoryId),
            "➕ Добавить сюда",
            product => Texts.ProductButtonLabel(product, fitsIntoLimit: true));

        await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.ProductsInCategoryHeader(categoryName, items.Count), keyboard, ct);
    }

    /// <summary>Начинаю добавление продукта в конкретную подкатегорию — дальше переиспользую обычный flow добавления.</summary>
    public async Task StartAddProductAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var categoryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        context.Reset();
        context.PendingProductCategoryId = categoryId == 0 ? null : categoryId;
        context.State = ConversationState.AwaitingFavoriteName;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskProductName, replyMarkup: null, ct);
    }

    /// <summary>Принимаю название будущего избранного продукта и спрашиваю тип порции.</summary>
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

        var sent = await _messenger.SendAsync(chatId, Texts.AskFavoriteServingMode, Keyboards.FavoriteServingModeChoice, ct);
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
    /// Принимаю БЖУ. Для фиксированной порции — спрашиваю ещё размер порции текстом (шаг необязательный).
    /// Для порции на 100 г — введённые числа и есть готовый эталон, сохраняю их сразу без лишних вопросов
    /// (вес конкретной съеденной порции спрошу потом, при добавлении в дневник).
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
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.FavoritesMenu, ct);
            return;
        }

        context.Apply(ProductDraft.FromMacros(context.ProductName, proteins, fats, carbs));

        if (context.MacrosPerHundredGrams)
        {
            await SaveAsync(chatId, userId, context, isFixedServing: false, editMessageId: null, ct);
            return;
        }

        context.State = ConversationState.AwaitingFavoriteServingSize;

        var sent = await _messenger.SendAsync(
            chatId,
            $"{Texts.ProductCard(context.ToDraft())}\n\n{Texts.AskServingSize}",
            Keyboards.SkipServingSize,
            ct);

        context.ActiveInlineMessageId = sent.MessageId;
    }

    /// <summary>Принимаю описание порции текстом и сохраняю продукт как фиксированную порцию.</summary>
    public async Task HandleServingSizeAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseServingSize(text, out var servingSize, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        context.ServingSize = servingSize;

        await SaveAsync(chatId, userId, context, isFixedServing: true, editMessageId: null, ct);
    }

    /// <summary>Пользователь пропустил шаг с порцией — сохраняю продукт как фиксированную порцию как есть.</summary>
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
        await SaveAsync(query.Message.Chat.Id, userId, context, isFixedServing: true, query.Message.MessageId, ct);
    }

    /// <summary>Начинаю создание новой подкатегории.</summary>
    public async Task StartCreateCategoryAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        context.Reset();
        context.State = ConversationState.AwaitingProductCategoryName;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskNewCategoryName, replyMarkup: null, ct);
    }

    /// <summary>Показываю список подкатегорий в режиме управления.</summary>
    public Task ShowCategoryManageListAsync(CallbackQuery query, CancellationToken ct) =>
        query.Message is null
            ? _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct)
            : AnswerThenShowCategoriesAsync(query, manageMode: true, ct);

    private async Task AnswerThenShowCategoriesAsync(CallbackQuery query, bool manageMode, CancellationToken ct)
    {
        await _messenger.AnswerAsync(query, ct: ct);
        await ShowProductCategoriesAsync(query.Message!.Chat.Id, query.From.Id, query.Message.MessageId, manageMode, ct);
    }

    /// <summary>Пользователь выбрал подкатегорию в режиме управления — показываю карточку с переименованием/удалением.</summary>
    public async Task ShowCategoryManageCardAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var categoryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var categories = await _favorites.GetProductCategoriesAsync(query.From.Id, ct);
        var category = categories.FirstOrDefault(c => c.Id == categoryId);

        if (category is null)
        {
            await _messenger.AnswerAsync(query, "Подкатегория не найдена.", showAlert: true, ct: ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id, query.Message.MessageId, Texts.CategoryManageCard(category), Keyboards.ProductCategoryManageActions(category.Id), ct);
    }

    /// <summary>Начинаю переименование подкатегории.</summary>
    public async Task StartRenameCategoryAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var categoryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        context.Reset();
        context.EditingProductCategoryId = categoryId;
        context.State = ConversationState.AwaitingProductCategoryName;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskCategoryRename, replyMarkup: null, ct);
    }

    /// <summary>Принимаю название подкатегории — новой или переименовываемой.</summary>
    public async Task HandleCategoryNameAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseProductName(text, out var name, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.FavoritesMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        var editingId = context.EditingProductCategoryId;
        context.Reset();

        if (editingId is { } categoryId)
        {
            var renamed = await _favorites.RenameProductCategoryAsync(userId, categoryId, name, ct);
            var text2 = renamed is null ? Texts.StaleDialog : Texts.CategoryRenamed(renamed.Name);
            await _messenger.SendAsync(chatId, text2, Keyboards.FavoritesMenu, ct);
            return;
        }

        var created = await _favorites.CreateProductCategoryAsync(userId, name, ct);
        await _messenger.SendAsync(chatId, Texts.CategoryCreated(created.Name), Keyboards.FavoritesMenu, ct);
    }

    /// <summary>Спрашиваю подтверждение перед удалением подкатегории.</summary>
    public async Task HandleCategoryDeleteRequestAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var categoryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var categories = await _favorites.GetProductCategoriesAsync(query.From.Id, ct);
        var category = categories.FirstOrDefault(c => c.Id == categoryId);

        if (category is null)
        {
            await _messenger.AnswerAsync(query, "Подкатегория не найдена.", showAlert: true, ct: ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id, query.Message.MessageId, Texts.ConfirmDeleteCategory(category), Keyboards.ProductCategoryDeleteConfirm(category.Id), ct);
    }

    /// <summary>Удаляю подкатегорию (продукты внутри остаются, теряют только привязку) и возвращаюсь к списку управления.</summary>
    public async Task HandleCategoryDeleteConfirmAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var categoryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var categories = await _favorites.GetProductCategoriesAsync(userId, ct);
        var name = categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "?";

        var deleted = await _favorites.DeleteProductCategoryAsync(userId, categoryId, ct);

        await _messenger.AnswerAsync(query, deleted ? "Удалил 🗑" : "Подкатегория уже удалена.", ct: ct);
        await ShowProductCategoriesAsync(query.Message.Chat.Id, userId, query.Message.MessageId, manageMode: true, ct);
    }

    // ------------------------------------------------------------------
    // Общие для всех групп: карточка продукта, редактирование, удаление, перенос
    // ------------------------------------------------------------------

    /// <summary>Показываю карточку продукта с действиями — набор зависит от его группы.</summary>
    public async Task ShowDetailsAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var favoriteId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var product = await _favorites.GetAsync(userId, favoriteId, ct);

        if (product is null)
        {
            await _messenger.AnswerAsync(query, "Продукт не найден — возможно, он уже удалён.", showAlert: true, ct: ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.FavoriteDetailsCard(product),
            Keyboards.FavoriteDetailsActions(product),
            ct);
    }

    /// <summary>
    /// Начинаю изменение КБЖУ уже сохранённого продукта — переиспользую сценарий добавления
    /// с готовым именем: он обновит существующую запись, а не создаст дубль. Для «Воды» — отдельная,
    /// более короткая ветка (всегда на 1 литр, без выбора типа порции).
    /// </summary>
    public async Task StartEditMacrosAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var favoriteId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var product = await _favorites.GetAsync(userId, favoriteId, ct);

        if (product is null)
        {
            await _messenger.AnswerAsync(query, "Продукт не найден — возможно, он уже удалён.", showAlert: true, ct: ct);
            return;
        }

        var context = _states.Get(userId);
        context.Reset();
        context.ProductName = product.Name;
        context.ActiveInlineMessageId = query.Message.MessageId;

        await _messenger.AnswerAsync(query, ct: ct);

        if (product.CategoryKind == FavoriteCategoryKind.Water)
        {
            context.State = ConversationState.AwaitingWaterMacros;
            await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskMacrosPerLiter, replyMarkup: null, ct);
            return;
        }

        context.State = ConversationState.AwaitingFavoriteMacrosMode;
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskFavoriteServingMode, Keyboards.FavoriteServingModeChoice, ct);
    }

    /// <summary>Переключаю тип порции без изменения КБЖУ и обновляю карточку.</summary>
    public async Task HandleToggleFixedServingAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var favoriteId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var product = await _favorites.GetAsync(userId, favoriteId, ct);

        if (product is null)
        {
            await _messenger.AnswerAsync(query, "Продукт не найден — возможно, он уже удалён.", showAlert: true, ct: ct);
            return;
        }

        var updated = await _favorites.SetFixedServingAsync(userId, favoriteId, !product.IsFixedServing, ct);

        await _messenger.AnswerAsync(query, updated.IsFixedServing ? "Теперь порция фиксированная 🍽" : "Теперь порция плавающая 📏", ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.FavoriteDetailsCard(updated),
            Keyboards.FavoriteDetailsActions(updated),
            ct);
    }

    /// <summary>Показываю выбор подкатегории для переноса продукта.</summary>
    public async Task StartMoveCategoryAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var favoriteId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var product = await _favorites.GetAsync(userId, favoriteId, ct);

        if (product is null)
        {
            await _messenger.AnswerAsync(query, "Продукт не найден — возможно, он уже удалён.", showAlert: true, ct: ct);
            return;
        }

        var categories = await _favorites.GetProductCategoriesAsync(userId, ct);

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id, query.Message.MessageId, Texts.AskMoveCategory(product), Keyboards.FavoriteMoveCategoryChoice(favoriteId, categories), ct);
    }

    /// <summary>Переношу продукт в выбранную подкатегорию (0 — «без категории») и показываю обновлённую карточку.</summary>
    public async Task HandleMoveCategoryConfirmAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null || parts.Length != 2 || !int.TryParse(parts[0], out var favoriteId) || !int.TryParse(parts[1], out var categoryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var updated = await _favorites.SetProductCategoryAsync(userId, favoriteId, categoryId == 0 ? null : categoryId, ct);

        if (updated is null)
        {
            await _messenger.AnswerAsync(query, "Продукт не найден — возможно, он уже удалён.", showAlert: true, ct: ct);
            return;
        }

        string? newCategoryName = null;
        if (updated.ProductCategoryId is { } newCategoryId)
        {
            var categories = await _favorites.GetProductCategoriesAsync(userId, ct);
            newCategoryName = categories.FirstOrDefault(c => c.Id == newCategoryId)?.Name;
        }

        await _messenger.AnswerAsync(query, "Перенёс 📂", ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            $"{Texts.FavoriteMoved(updated, newCategoryName)}\n\n{Texts.FavoriteDetailsCard(updated)}",
            Keyboards.FavoriteDetailsActions(updated),
            ct);
    }

    /// <summary>Показываю страницу списка удаления (все группы вместе — список для удаления не обязан быть категоризирован).</summary>
    public async Task ShowDeleteListAsync(long chatId, long userId, int page, int? editMessageId, CancellationToken ct)
    {
        var favorites = await _favorites.GetAllAsync(userId, ct);

        if (favorites.Count == 0)
        {
            await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.EmptyFavorites, Keyboards.ToMenuOnly, ct);
            return;
        }

        var keyboard = Keyboards.ProductPage(
            favorites,
            page,
            Callbacks.DeleteFavorite,
            Callbacks.DeletePage,
            product => Texts.ProductButtonLabel(product, fitsIntoLimit: true));

        await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.DeleteListHeader(favorites.Count), keyboard, ct);
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

    /// <summary>Общая точка сохранения обычного продукта: и для введённой порции, и для пропущенного шага, и для порции на 100 г.</summary>
    private async Task SaveAsync(
        long chatId,
        long userId,
        ConversationContext context,
        bool isFixedServing,
        int? editMessageId,
        CancellationToken ct)
    {
        var draft = context.ToDraft();
        var productCategoryId = context.PendingProductCategoryId;
        var (created, product) = await _favorites.AddOrUpdateAsync(
            userId, draft, isFixedServing, ct, categoryKind: FavoriteCategoryKind.Product, productCategoryId: productCategoryId);

        context.Reset();

        await _messenger.SendOrEditAsync(
            chatId,
            editMessageId,
            Texts.FavoriteSaved(product, created),
            Keyboards.AfterFavoriteSaved,
            ct);

        _logger.LogInformation(
            "Пользователь {UserId} сохранил в избранное «{ProductName}» ({Calories} ккал)",
            userId, product.Name, product.Calories);
    }
}

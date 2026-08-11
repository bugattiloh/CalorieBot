using CalorieBot.Api.Bot;
using CalorieBot.Api.Bot.Scenarios;
using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Data.Entities;
using CalorieBot.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CalorieBot.Tests.Scenarios;

/// <summary>Сценарий «⭐ Избранное»: три группы (Вода/Готовые блюда/Продукты с подкатегориями), добавление, удаление, перенос.</summary>
public class FavoritesScenarioTests
{
    private const long ChatId = 100;
    private const long UserId = 1;

    private sealed record Harness(FavoritesScenario Scenario, RecordingBotClient Bot, IConversationStateStore States, Mock<IFavoriteProductService> Favorites);

    private static Harness CreateHarness()
    {
        var bot = new RecordingBotClient();
        var messenger = new BotMessenger(bot.Client, NullLogger<BotMessenger>.Instance);
        var states = new MemoryConversationStateStore(new MemoryCache(new MemoryCacheOptions()));
        var favorites = new Mock<IFavoriteProductService>();

        var scenario = new FavoritesScenario(messenger, favorites.Object, states, NullLogger<FavoritesScenario>.Instance);

        return new Harness(scenario, bot, states, favorites);
    }

    private static CallbackQuery BuildCallbackQuery(string data = "skp") => new()
    {
        Id = "cb-1",
        From = new User { Id = UserId, FirstName = "Test" },
        Message = new Message { Id = 55, Chat = new Chat { Id = ChatId } },
        Data = data
    };

    // ------------------------------------------------------------------
    // Вода
    // ------------------------------------------------------------------

    [Fact]
    public async Task ShowWaterListAsync_SeedsWaterAndListsItems()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetByCategoryAsync(UserId, FavoriteCategoryKind.Water, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FavoriteProduct { Id = 1, Name = "Вода", CategoryKind = FavoriteCategoryKind.Water } });

        await h.Scenario.ShowWaterListAsync(ChatId, UserId, page: 0, editMessageId: null, CancellationToken.None);

        h.Favorites.Verify(f => f.EnsureWaterSeedAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Sent);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Sent[0].ReplyMarkup);
        Assert.Contains(keyboard.InlineKeyboard.SelectMany(row => row), b => b.Text.Contains("Вода"));
    }

    [Fact]
    public async Task StartAddWaterAsync_AwaitsWaterName()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("wad");

        await h.Scenario.StartAddWaterAsync(query, CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingWaterName, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleWaterNameAsync_WithValidName_AsksMacrosPerLiter()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingWaterName;

        await h.Scenario.HandleWaterNameAsync(ChatId, UserId, "Морс", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal("Морс", context.ProductName);
        Assert.Equal(ConversationState.AwaitingWaterMacros, context.State);
        Assert.Contains("литр", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task HandleWaterMacrosAsync_SavesWithWaterCategoryAndFloatingServing()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Морс";
        context.State = ConversationState.AwaitingWaterMacros;

        h.Favorites.Setup(f => f.AddOrUpdateAsync(
                UserId, It.IsAny<ProductDraft>(), false, It.IsAny<CancellationToken>(),
                FavoriteCategoryKind.Water, null))
            .ReturnsAsync((true, new FavoriteProduct { Id = 2, Name = "Морс", CategoryKind = FavoriteCategoryKind.Water }));

        await h.Scenario.HandleWaterMacrosAsync(ChatId, UserId, "0.1 0 5", CancellationToken.None);

        h.Favorites.Verify(f => f.AddOrUpdateAsync(
                UserId, It.IsAny<ProductDraft>(), false, It.IsAny<CancellationToken>(), FavoriteCategoryKind.Water, null),
            Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
    }

    // ------------------------------------------------------------------
    // Продукты — подкатегории
    // ------------------------------------------------------------------

    [Fact]
    public async Task ShowProductCategoriesAsync_ListsCategoriesAsButtons()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetProductCategoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductCategory { Id = 1, Name = "Белковые продукты", IsBuiltIn = true } });
        h.Favorites.Setup(f => f.GetByCategoryAsync(UserId, FavoriteCategoryKind.Product, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FavoriteProduct>());

        await h.Scenario.ShowProductCategoriesAsync(ChatId, UserId, editMessageId: null, manageMode: false, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Sent[0].ReplyMarkup);
        Assert.Contains(keyboard.InlineKeyboard.SelectMany(row => row), b => b.Text.Contains("Белковые продукты"));
    }

    [Fact]
    public async Task HandleProductCategoryPickAsync_ShowsItemsInThatCategory()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetByCategoryAsync(UserId, FavoriteCategoryKind.Product, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FavoriteProduct { Id = 9, Name = "Гречка", CategoryKind = FavoriteCategoryKind.Product, ProductCategoryId = 3 } });
        h.Favorites.Setup(f => f.GetProductCategoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductCategory { Id = 3, Name = "Зерновые и крахмалистые" } });

        var query = BuildCallbackQuery("pcat:3");
        await h.Scenario.HandleProductCategoryPickAsync(query, argument: "3", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        Assert.Contains("Гречка", string.Join(" ", ((InlineKeyboardMarkup)h.Bot.Edited[0].ReplyMarkup!).InlineKeyboard.SelectMany(r => r).Select(b => b.Text)));
        Assert.Contains("Зерновые и крахмалистые", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task StartAddProductAsync_SetsPendingCategoryAndAwaitsName()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("pah:3");

        await h.Scenario.StartAddProductAsync(query, argument: "3", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(3, context.PendingProductCategoryId);
        Assert.Equal(ConversationState.AwaitingFavoriteName, context.State);
    }

    [Fact]
    public async Task StartAddProductAsync_WithZeroArgument_LeavesPendingCategoryNull()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("pah:0");

        await h.Scenario.StartAddProductAsync(query, argument: "0", CancellationToken.None);

        Assert.Null(h.States.Get(UserId).PendingProductCategoryId);
    }

    [Fact]
    public async Task HandleNameAsync_WithValidName_AsksServingMode()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingFavoriteName;

        await h.Scenario.HandleNameAsync(ChatId, UserId, "Творог", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal("Творог", context.ProductName);
        Assert.Equal(ConversationState.AwaitingFavoriteMacrosMode, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleMacrosModeAsync_WithPerHundred_MarksContextAndAsksMacros()
    {
        var h = CreateHarness();
        h.States.Get(UserId).ProductName = "Творог";
        h.States.Get(UserId).State = ConversationState.AwaitingFavoriteMacrosMode;

        var query = BuildCallbackQuery(data: "fmm:100");
        await h.Scenario.HandleMacrosModeAsync(query, argument: "100", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.True(context.MacrosPerHundredGrams);
        Assert.Equal(ConversationState.AwaitingFavoriteMacros, context.State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleMacrosAsync_PerHundredMode_SavesWithPendingCategory()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Творог";
        context.MacrosPerHundredGrams = true;
        context.PendingProductCategoryId = 3;
        context.State = ConversationState.AwaitingFavoriteMacros;

        h.Favorites.Setup(f => f.AddOrUpdateAsync(
                UserId, It.IsAny<ProductDraft>(), false, It.IsAny<CancellationToken>(),
                FavoriteCategoryKind.Product, 3))
            .ReturnsAsync((true, new FavoriteProduct { Id = 1, UserId = UserId, Name = "Творог", Calories = 113, IsFixedServing = false }));

        await h.Scenario.HandleMacrosAsync(ChatId, UserId, "18 5 3", CancellationToken.None);

        h.Favorites.Verify(f => f.AddOrUpdateAsync(
                UserId,
                It.Is<ProductDraft>(d => d.Proteins == 18 && d.Fats == 5 && d.Carbs == 3),
                /* isFixedServing */ false,
                It.IsAny<CancellationToken>(),
                FavoriteCategoryKind.Product,
                3),
            Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleMacrosAsync_WithValidMacros_AsksForServingSize()
    {
        var h = CreateHarness();
        h.States.Get(UserId).ProductName = "Творог";
        h.States.Get(UserId).State = ConversationState.AwaitingFavoriteMacros;

        await h.Scenario.HandleMacrosAsync(ChatId, UserId, "18 5 3", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(ConversationState.AwaitingFavoriteServingSize, context.State);
        Assert.NotNull(context.ActiveInlineMessageId);
    }

    [Fact]
    public async Task HandleServingSizeAsync_SavesProductAsFixedServing()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Творог", 18, 5, 3));
        context.State = ConversationState.AwaitingFavoriteServingSize;

        h.Favorites.Setup(f => f.AddOrUpdateAsync(
                UserId, It.IsAny<ProductDraft>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(),
                It.IsAny<FavoriteCategoryKind>(), It.IsAny<int?>()))
            .ReturnsAsync((true, new FavoriteProduct { Id = 1, UserId = UserId, Name = "Творог", Calories = 113, ServingSize = "200 г", IsFixedServing = true }));

        await h.Scenario.HandleServingSizeAsync(ChatId, UserId, "200 г", CancellationToken.None);

        h.Favorites.Verify(f => f.AddOrUpdateAsync(
                UserId,
                It.Is<ProductDraft>(d => d.ServingSize == "200 г"),
                /* isFixedServing */ true,
                It.IsAny<CancellationToken>(),
                It.IsAny<FavoriteCategoryKind>(),
                It.IsAny<int?>()),
            Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleSkipServingSizeAsync_SavesProductWithoutServingSize()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Творог", 18, 5, 3));
        context.ActiveInlineMessageId = 77;

        h.Favorites.Setup(f => f.AddOrUpdateAsync(
                UserId, It.IsAny<ProductDraft>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(),
                It.IsAny<FavoriteCategoryKind>(), It.IsAny<int?>()))
            .ReturnsAsync((true, new FavoriteProduct { Id = 1, UserId = UserId, Name = "Творог", Calories = 113, IsFixedServing = true }));

        var query = BuildCallbackQuery();

        await h.Scenario.HandleSkipServingSizeAsync(query, CancellationToken.None);

        h.Favorites.Verify(f => f.AddOrUpdateAsync(
                UserId,
                It.Is<ProductDraft>(d => d.ServingSize == null),
                /* isFixedServing */ true,
                It.IsAny<CancellationToken>(),
                It.IsAny<FavoriteCategoryKind>(),
                It.IsAny<int?>()),
            Times.Once);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task StartCreateCategoryAsync_AwaitsCategoryName()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("pcn");

        await h.Scenario.StartCreateCategoryAsync(query, CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(ConversationState.AwaitingProductCategoryName, context.State);
        Assert.Null(context.EditingProductCategoryId);
    }

    [Fact]
    public async Task StartRenameCategoryAsync_SetsEditingIdAndAwaitsName()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("pcr:3");

        await h.Scenario.StartRenameCategoryAsync(query, argument: "3", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(3, context.EditingProductCategoryId);
        Assert.Equal(ConversationState.AwaitingProductCategoryName, context.State);
    }

    [Fact]
    public async Task HandleCategoryNameAsync_WithoutEditingId_CreatesNewCategory()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingProductCategoryName;
        h.Favorites.Setup(f => f.CreateProductCategoryAsync(UserId, "Напитки", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductCategory { Id = 10, Name = "Напитки" });

        await h.Scenario.HandleCategoryNameAsync(ChatId, UserId, "Напитки", CancellationToken.None);

        h.Favorites.Verify(f => f.CreateProductCategoryAsync(UserId, "Напитки", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Напитки", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task HandleCategoryNameAsync_WithEditingId_RenamesCategory()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.State = ConversationState.AwaitingProductCategoryName;
        context.EditingProductCategoryId = 3;

        h.Favorites.Setup(f => f.RenameProductCategoryAsync(UserId, 3, "Крупы", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductCategory { Id = 3, Name = "Крупы" });

        await h.Scenario.HandleCategoryNameAsync(ChatId, UserId, "Крупы", CancellationToken.None);

        h.Favorites.Verify(f => f.RenameProductCategoryAsync(UserId, 3, "Крупы", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Крупы", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task HandleCategoryDeleteConfirmAsync_DeletesAndReturnsToManageList()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetProductCategoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductCategory { Id = 3, Name = "Жиры" } });
        h.Favorites.Setup(f => f.DeleteProductCategoryAsync(UserId, 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        h.Favorites.Setup(f => f.GetByCategoryAsync(UserId, FavoriteCategoryKind.Product, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FavoriteProduct>());

        var query = BuildCallbackQuery("pcdy:3");
        await h.Scenario.HandleCategoryDeleteConfirmAsync(query, argument: "3", CancellationToken.None);

        h.Favorites.Verify(f => f.DeleteProductCategoryAsync(UserId, 3, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
    }

    // ------------------------------------------------------------------
    // Общие: карточка, редактирование, удаление, перенос
    // ------------------------------------------------------------------

    [Fact]
    public async Task ShowDetailsAsync_WithExistingProduct_ShowsCardAndActions()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", Calories = 300, IsFixedServing = false });

        var query = BuildCallbackQuery(data: "fd:5");
        await h.Scenario.ShowDetailsAsync(query, argument: "5", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        Assert.Contains("Рис", h.Bot.Edited[0].Text);
        Assert.Contains("100 г", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task StartEditMacrosAsync_ForProduct_PrefillsNameAndAsksServingMode()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", Calories = 300, CategoryKind = FavoriteCategoryKind.Product });

        var query = BuildCallbackQuery(data: "fem:5");
        await h.Scenario.StartEditMacrosAsync(query, argument: "5", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal("Рис", context.ProductName);
        Assert.Equal(ConversationState.AwaitingFavoriteMacrosMode, context.State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task StartEditMacrosAsync_ForWater_SkipsServingModeAndAsksMacrosPerLiter()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Вода", Calories = 0, CategoryKind = FavoriteCategoryKind.Water });

        var query = BuildCallbackQuery(data: "fem:5");
        await h.Scenario.StartEditMacrosAsync(query, argument: "5", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal("Вода", context.ProductName);
        Assert.Equal(ConversationState.AwaitingWaterMacros, context.State);
        Assert.Contains("литр", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleToggleFixedServingAsync_FlipsFlagAndRefreshesCard()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", Calories = 300, IsFixedServing = true });
        h.Favorites.Setup(f => f.SetFixedServingAsync(UserId, 5, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", Calories = 300, IsFixedServing = false });

        var query = BuildCallbackQuery(data: "fet:5");
        await h.Scenario.HandleToggleFixedServingAsync(query, argument: "5", CancellationToken.None);

        h.Favorites.Verify(f => f.SetFixedServingAsync(UserId, 5, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task StartMoveCategoryAsync_ShowsCategoryChoice()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", CategoryKind = FavoriteCategoryKind.Product });
        h.Favorites.Setup(f => f.GetProductCategoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductCategory { Id = 3, Name = "Зерновые и крахмалистые" } });

        var query = BuildCallbackQuery("fmc:5");
        await h.Scenario.StartMoveCategoryAsync(query, argument: "5", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Edited[0].ReplyMarkup);
        Assert.Contains(keyboard.InlineKeyboard.SelectMany(row => row), b => b.Text.Contains("Зерновые"));
    }

    [Fact]
    public async Task HandleMoveCategoryConfirmAsync_MovesAndShowsUpdatedCard()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.SetProductCategoryAsync(UserId, 5, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", CategoryKind = FavoriteCategoryKind.Product, ProductCategoryId = 3 });
        h.Favorites.Setup(f => f.GetProductCategoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductCategory { Id = 3, Name = "Зерновые и крахмалистые" } });

        var query = BuildCallbackQuery("fmcc:5:3");
        await h.Scenario.HandleMoveCategoryConfirmAsync(query, argument: "5:3", CancellationToken.None);

        h.Favorites.Verify(f => f.SetProductCategoryAsync(UserId, 5, 3, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
        Assert.Contains("Зерновые и крахмалистые", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleDeleteRequestAsync_WithExistingProduct_AsksForConfirmation()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", Calories = 300 });

        var query = BuildCallbackQuery(data: "df:5");

        await h.Scenario.HandleDeleteRequestAsync(query, argument: "5", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        Assert.Contains("Удалить", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleDeleteConfirmAsync_RemovesProductAndShowsRemainingList()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.DeleteAsync(UserId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 5, Name = "Рис", Calories = 300 });
        h.Favorites.Setup(f => f.GetAllAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FavoriteProduct>());

        var query = BuildCallbackQuery(data: "dfy:5");

        await h.Scenario.HandleDeleteConfirmAsync(query, argument: "5", CancellationToken.None);

        h.Favorites.Verify(f => f.DeleteAsync(UserId, 5, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
        Assert.Contains("Рис", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleDeleteConfirmAsync_WhenAlreadyDeleted_DoesNotThrow()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.DeleteAsync(UserId, 5, It.IsAny<CancellationToken>())).ReturnsAsync((FavoriteProduct?)null);
        h.Favorites.Setup(f => f.GetAllAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FavoriteProduct>());

        var query = BuildCallbackQuery(data: "dfy:5");

        await h.Scenario.HandleDeleteConfirmAsync(query, argument: "5", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
    }
}

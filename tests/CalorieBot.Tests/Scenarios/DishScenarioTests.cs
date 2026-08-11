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

/// <summary>Сценарий «🍲 Готовые блюда»: создание блюда и управление составом ингредиентов.</summary>
public class DishScenarioTests
{
    private const long ChatId = 100;
    private const long UserId = 1;

    private sealed record Harness(DishScenario Scenario, RecordingBotClient Bot, IConversationStateStore States, Mock<IFavoriteProductService> Favorites);

    private static Harness CreateHarness()
    {
        var bot = new RecordingBotClient();
        var messenger = new BotMessenger(bot.Client, NullLogger<BotMessenger>.Instance);
        var states = new MemoryConversationStateStore(new MemoryCache(new MemoryCacheOptions()));
        var favorites = new Mock<IFavoriteProductService>();

        var scenario = new DishScenario(messenger, favorites.Object, states, NullLogger<DishScenario>.Instance);

        return new Harness(scenario, bot, states, favorites);
    }

    private static CallbackQuery BuildCallbackQuery(string data) => new()
    {
        Id = "cb-1",
        From = new User { Id = UserId, FirstName = "Test" },
        Message = new Message { Id = 55, Chat = new Chat { Id = ChatId } },
        Data = data
    };

    [Fact]
    public async Task ShowListAsync_ListsDishesAsClickableButtons()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetByCategoryAsync(UserId, FavoriteCategoryKind.Dish, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FavoriteProduct { Id = 1, Name = "Овсянка с бананом", CategoryKind = FavoriteCategoryKind.Dish } });

        await h.Scenario.ShowListAsync(ChatId, UserId, page: 0, editMessageId: null, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Sent[0].ReplyMarkup);
        Assert.Contains(keyboard.InlineKeyboard.SelectMany(row => row), b => b.Text.Contains("Овсянка"));
    }

    [Fact]
    public async Task ShowListAsync_KeyboardHasBackToFavoritesButton_NotToMenu()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetByCategoryAsync(UserId, FavoriteCategoryKind.Dish, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FavoriteProduct>());

        await h.Scenario.ShowListAsync(ChatId, UserId, page: 0, editMessageId: null, CancellationToken.None);

        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Sent[0].ReplyMarkup);
        var buttons = keyboard.InlineKeyboard.SelectMany(row => row).ToList();
        Assert.Contains(buttons, b => b.CallbackData == "favm");
        Assert.DoesNotContain(buttons, b => b.CallbackData == "menu");
    }

    [Fact]
    public async Task StartCreateAsync_AwaitsDishName()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("dad");

        await h.Scenario.StartCreateAsync(query, CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingDishName, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleNameAsync_CreatesEmptyDishAndShowsCard()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingDishName;
        h.Favorites.Setup(f => f.CreateDishAsync(UserId, "Салат", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 7, Name = "Салат", CategoryKind = FavoriteCategoryKind.Dish });

        await h.Scenario.HandleNameAsync(ChatId, UserId, "Салат", CancellationToken.None);

        h.Favorites.Verify(f => f.CreateDishAsync(UserId, "Салат", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Contains("Салат", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task ShowDetailsAsync_WithExistingDish_ShowsCardWithIngredientButtons()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 7, Name = "Салат", CategoryKind = FavoriteCategoryKind.Dish, Calories = 50 });
        h.Favorites.Setup(f => f.GetDishIngredientsAsync(UserId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DishIngredient { Id = 1, Name = "Огурец", Calories = 15 } });

        var query = BuildCallbackQuery("dd:7");
        await h.Scenario.ShowDetailsAsync(query, argument: "7", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Edited[0].ReplyMarkup);
        Assert.Contains(keyboard.InlineKeyboard.SelectMany(row => row), b => b.Text.Contains("Огурец"));
    }

    [Fact]
    public async Task StartAddIngredientAsync_ShowsSourceChoice()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("dai:7");

        await h.Scenario.StartAddIngredientAsync(query, argument: "7", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Edited[0].ReplyMarkup);
        Assert.Contains(keyboard.InlineKeyboard.SelectMany(row => row), b => b.Text.Contains("избранного"));
    }

    [Fact]
    public async Task StartAddCustomIngredientAsync_SetsEditingDishIdAndAwaitsName()
    {
        var h = CreateHarness();
        var query = BuildCallbackQuery("dic:7");

        await h.Scenario.StartAddCustomIngredientAsync(query, argument: "7", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(7, context.EditingDishId);
        Assert.Equal(ConversationState.AwaitingDishIngredientName, context.State);
    }

    [Fact]
    public async Task ShowFavoriteIngredientPickerAsync_ListsProductFavoritesOnly()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAllAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new FavoriteProduct { Id = 1, Name = "Гречка", CategoryKind = FavoriteCategoryKind.Product },
                new FavoriteProduct { Id = 2, Name = "Вода", CategoryKind = FavoriteCategoryKind.Water },
                new FavoriteProduct { Id = 3, Name = "Салат", CategoryKind = FavoriteCategoryKind.Dish }
            });

        var query = BuildCallbackQuery("diff:7");
        await h.Scenario.ShowFavoriteIngredientPickerAsync(query, argument: "7", CancellationToken.None);

        Assert.Single(h.Bot.Edited);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(h.Bot.Edited[0].ReplyMarkup);
        var labels = keyboard.InlineKeyboard.SelectMany(row => row).Select(b => b.Text).ToList();
        Assert.Contains(labels, l => l.Contains("Гречка"));
        Assert.DoesNotContain(labels, l => l.Contains("Вода"));
        Assert.DoesNotContain(labels, l => l.Contains("Салат"));
    }

    [Fact]
    public async Task HandlePickFavoriteIngredientAsync_WithFixedServing_AddsDirectly()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 1, Name = "Батончик", IsFixedServing = true, CategoryKind = FavoriteCategoryKind.Product, Calories = 200 });
        h.Favorites.Setup(f => f.AddDishIngredientAsync(UserId, 7, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 7, Name = "Салат", CategoryKind = FavoriteCategoryKind.Dish, Calories = 200 });
        h.Favorites.Setup(f => f.GetDishIngredientsAsync(UserId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DishIngredient { Id = 1, Name = "Батончик", Calories = 200 } });

        var query = BuildCallbackQuery("dipf:7:1");
        await h.Scenario.HandlePickFavoriteIngredientAsync(query, argument: "7:1", CancellationToken.None);

        h.Favorites.Verify(f => f.AddDishIngredientAsync(UserId, 7, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
        Assert.Contains("Салат", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandlePickFavoriteIngredientAsync_WithFloatingServing_AsksForGrams()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 1, Name = "Рис", IsFixedServing = false, CategoryKind = FavoriteCategoryKind.Product, Proteins = 7, Fats = 1, Carbs = 78 });

        var query = BuildCallbackQuery("dipf:7:1");
        await h.Scenario.HandlePickFavoriteIngredientAsync(query, argument: "7:1", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(7, context.EditingDishId);
        Assert.Equal("Рис", context.ProductName);
        Assert.Equal(ConversationState.AwaitingDishIngredientGrams, context.State);
        h.Favorites.Verify(f => f.AddDishIngredientAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleIngredientGramsAsync_ScalesMacrosAndAddsIngredient()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.EditingDishId = 7;
        context.ProductName = "Рис";
        context.Proteins = 7;
        context.Fats = 1;
        context.Carbs = 78;
        context.State = ConversationState.AwaitingDishIngredientGrams;

        h.Favorites.Setup(f => f.AddDishIngredientAsync(UserId, 7, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 7, Name = "Салат", CategoryKind = FavoriteCategoryKind.Dish });
        h.Favorites.Setup(f => f.GetDishIngredientsAsync(UserId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DishIngredient>());

        await h.Scenario.HandleIngredientGramsAsync(ChatId, UserId, "150", CancellationToken.None);

        h.Favorites.Verify(f => f.AddDishIngredientAsync(
                UserId, 7, It.Is<ProductDraft>(d => d.Proteins == 10.5m && d.ServingSize == "150 г"), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Null(h.States.Get(UserId).EditingDishId);
    }

    [Fact]
    public async Task HandleIngredientNameAsync_StoresNameAndAsksMacros()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.EditingDishId = 7;
        context.State = ConversationState.AwaitingDishIngredientName;

        await h.Scenario.HandleIngredientNameAsync(ChatId, UserId, "Огурец", CancellationToken.None);

        Assert.Equal("Огурец", context.ProductName);
        Assert.Equal(ConversationState.AwaitingDishIngredientMacros, context.State);
    }

    [Fact]
    public async Task HandleIngredientMacrosAsync_AddsIngredientAndShowsUpdatedCard()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.EditingDishId = 7;
        context.ProductName = "Огурец";
        context.State = ConversationState.AwaitingDishIngredientMacros;

        h.Favorites.Setup(f => f.AddDishIngredientAsync(UserId, 7, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 7, Name = "Салат", CategoryKind = FavoriteCategoryKind.Dish, Calories = 15 });
        h.Favorites.Setup(f => f.GetDishIngredientsAsync(UserId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DishIngredient { Id = 1, Name = "Огурец", Calories = 15 } });

        await h.Scenario.HandleIngredientMacrosAsync(ChatId, UserId, "1 0 3", CancellationToken.None);

        h.Favorites.Verify(f => f.AddDishIngredientAsync(UserId, 7, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(h.States.Get(UserId).EditingDishId);
        Assert.Contains("Салат", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task HandleIngredientDeleteConfirmAsync_RemovesIngredientAndRefreshesCard()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FavoriteProduct { Id = 7, Name = "Салат", CategoryKind = FavoriteCategoryKind.Dish, Calories = 0 });
        h.Favorites.Setup(f => f.GetDishIngredientsAsync(UserId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DishIngredient>());

        var query = BuildCallbackQuery("didc:7:1");
        await h.Scenario.HandleIngredientDeleteConfirmAsync(query, argument: "7:1", CancellationToken.None);

        h.Favorites.Verify(f => f.RemoveDishIngredientAsync(UserId, 7, 1, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
    }
}

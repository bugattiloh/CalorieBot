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

namespace CalorieBot.Tests.Scenarios;

/// <summary>Сценарий «⭐ Любимые продукты»: добавление, просмотр постранично и удаление.</summary>
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

    [Fact]
    public async Task StartAddAsync_AwaitsProductName()
    {
        var h = CreateHarness();

        await h.Scenario.StartAddAsync(ChatId, UserId, CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingFavoriteName, h.States.Get(UserId).State);
    }

    [Fact]
    public async Task HandleNameAsync_WithValidName_AsksMacrosMode()
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
    public async Task HandleMacrosAsync_PerHundredMode_StoresRawValuesAndAsksForGrams()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Творог";
        context.MacrosPerHundredGrams = true;
        context.State = ConversationState.AwaitingFavoriteMacros;

        await h.Scenario.HandleMacrosAsync(ChatId, UserId, "18 5 3", CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingFavoriteServingGrams, context.State);
        Assert.Equal(18, context.Proteins);
        Assert.Equal(5, context.Fats);
        Assert.Equal(3, context.Carbs);
    }

    [Fact]
    public async Task HandleServingGramsAsync_PerHundredMode_ScalesAndSavesWithoutAskingServingSizeAgain()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Творог";
        context.MacrosPerHundredGrams = true;
        context.Proteins = 18;
        context.Fats = 5;
        context.Carbs = 3;
        context.State = ConversationState.AwaitingFavoriteServingGrams;

        h.Favorites.Setup(f => f.AddOrUpdateAsync(UserId, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new FavoriteProduct { Id = 1, UserId = UserId, Name = "Творог", Calories = 226, ServingSize = "200 г" }));

        await h.Scenario.HandleServingGramsAsync(ChatId, UserId, "200", CancellationToken.None);

        // На 200 г: Б 36, Ж 10, У 6.
        h.Favorites.Verify(f => f.AddOrUpdateAsync(
                UserId,
                It.Is<ProductDraft>(d => d.Proteins == 36 && d.Fats == 10 && d.Carbs == 6 && d.ServingSize == "200 г"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
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
    public async Task HandleServingSizeAsync_SavesProductThroughFavoritesService()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Творог", 18, 5, 3));
        context.State = ConversationState.AwaitingFavoriteServingSize;

        h.Favorites.Setup(f => f.AddOrUpdateAsync(UserId, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new FavoriteProduct { Id = 1, UserId = UserId, Name = "Творог", Calories = 113, ServingSize = "200 г" }));

        await h.Scenario.HandleServingSizeAsync(ChatId, UserId, "200 г", CancellationToken.None);

        h.Favorites.Verify(f => f.AddOrUpdateAsync(UserId, It.Is<ProductDraft>(d => d.ServingSize == "200 г"), It.IsAny<CancellationToken>()), Times.Once);
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

        h.Favorites.Setup(f => f.AddOrUpdateAsync(UserId, It.IsAny<ProductDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new FavoriteProduct { Id = 1, UserId = UserId, Name = "Творог", Calories = 113 }));

        var query = BuildCallbackQuery();

        await h.Scenario.HandleSkipServingSizeAsync(query, CancellationToken.None);

        h.Favorites.Verify(f => f.AddOrUpdateAsync(UserId, It.Is<ProductDraft>(d => d.ServingSize == null), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task ShowListAsync_WhenEmpty_SendsEmptyFavoritesMessage()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAllAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FavoriteProduct>());

        await h.Scenario.ShowListAsync(ChatId, UserId, page: 0, editMessageId: null, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Contains("пусто", h.Bot.Sent[0].Text);
    }

    [Fact]
    public async Task ShowListAsync_WithProducts_ListsThemInMessage()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAllAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FavoriteProduct> { new() { Id = 1, Name = "Рис", Calories = 300 } });

        await h.Scenario.ShowListAsync(ChatId, UserId, page: 0, editMessageId: null, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Contains("Рис", h.Bot.Sent[0].Text);
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

    private static CallbackQuery BuildCallbackQuery(string data = "skp") => new()
    {
        Id = "cb-1",
        From = new User { Id = UserId, FirstName = "Test" },
        Message = new Message { Id = 55, Chat = new Chat { Id = ChatId } },
        Data = data
    };
}

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

/// <summary>Сценарий «🍽 Добавить прием пищи»: и из избранного, и новым продуктом.</summary>
public class MealScenarioTests
{
    private const long ChatId = 100;
    private const long UserId = 1;

    private sealed record Harness(MealScenario Scenario, RecordingBotClient Bot, IConversationStateStore States)
    {
        public Mock<IFavoriteProductService> Favorites { get; init; } = null!;
        public Mock<IFoodLogService> FoodLog { get; init; } = null!;
        public Mock<IProgressService> Progress { get; init; } = null!;
    }

    private static Harness CreateHarness()
    {
        var bot = new RecordingBotClient();
        var messenger = new BotMessenger(bot.Client, NullLogger<BotMessenger>.Instance);
        var states = new MemoryConversationStateStore(new MemoryCache(new MemoryCacheOptions()));
        var favorites = new Mock<IFavoriteProductService>();
        var foodLog = new Mock<IFoodLogService>();
        // По умолчанию цикл пуст — тесты дубликатов сами переопределяют этот setup.
        foodLog.Setup(f => f.GetCurrentCycleAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FoodLogEntry>());
        var progress = new Mock<IProgressService>();

        var scenario = new MealScenario(
            messenger, favorites.Object, foodLog.Object, progress.Object, states, NullLogger<MealScenario>.Instance);

        return new Harness(scenario, bot, states) { Favorites = favorites, FoodLog = foodLog, Progress = progress };
    }

    private static DailyProgress EmptyProgress(int limit = 2000) => new()
    {
        CycleStartedAt = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
        TrackingMode = CalorieTrackingMode.Calories,
        CalorieLimit = limit,
        ConsumedCalories = 0
    };

    [Fact]
    public async Task ShowMenuAsync_SendsMealSourceChoice_AndResetsState()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingCalorieLimit; // симулирую незавершённый другой диалог

        await h.Scenario.ShowMenuAsync(ChatId, UserId, CancellationToken.None);

        Assert.Single(h.Bot.Sent);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
    }

    [Fact]
    public async Task StartNewProductAsync_AsksForNameAndAwaitsIt()
    {
        var h = CreateHarness();

        await h.Scenario.StartNewProductAsync(ChatId, UserId, CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingMealProductName, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleNameAsync_WithValidName_StoresItAndAsksMacrosMode()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingMealProductName;

        await h.Scenario.HandleNameAsync(ChatId, UserId, "Гречка", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal("Гречка", context.ProductName);
        Assert.Equal(ConversationState.AwaitingMealMacrosMode, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleMacrosModeAsync_WithWholePortion_AsksForMacrosOnWholePortion()
    {
        var h = CreateHarness();
        h.States.Get(UserId).ProductName = "Гречка";
        h.States.Get(UserId).State = ConversationState.AwaitingMealMacrosMode;

        var query = BuildCallbackQuery(data: "mmm:full");
        await h.Scenario.HandleMacrosModeAsync(query, argument: "full", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.False(context.MacrosPerHundredGrams);
        Assert.Equal(ConversationState.AwaitingMealProductMacros, context.State);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleMacrosModeAsync_WithPerHundred_MarksContextAccordingly()
    {
        var h = CreateHarness();
        h.States.Get(UserId).ProductName = "Гречка";
        h.States.Get(UserId).State = ConversationState.AwaitingMealMacrosMode;

        var query = BuildCallbackQuery(data: "mmm:100");
        await h.Scenario.HandleMacrosModeAsync(query, argument: "100", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.True(context.MacrosPerHundredGrams);
        Assert.Equal(ConversationState.AwaitingMealProductMacros, context.State);
    }

    [Fact]
    public async Task HandleMacrosAsync_PerHundredMode_StoresRawValuesAndAsksForGrams()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Гречка";
        context.MacrosPerHundredGrams = true;
        context.State = ConversationState.AwaitingMealProductMacros;

        await h.Scenario.HandleMacrosAsync(ChatId, UserId, "12 5 30", CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingMealServingGrams, context.State);
        Assert.Equal(12, context.Proteins);
        Assert.Equal(5, context.Fats);
        Assert.Equal(30, context.Carbs);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleServingGramsAsync_ScalesMacrosFromHundredGramsAndOffersToSaveFavorite()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Гречка";
        context.MacrosPerHundredGrams = true;
        context.Proteins = 12;
        context.Fats = 5;
        context.Carbs = 30;
        context.State = ConversationState.AwaitingMealServingGrams;

        await h.Scenario.HandleServingGramsAsync(ChatId, UserId, "150", CancellationToken.None);

        // На 150 г: Б 18, Ж 7.5, У 45 -> калории 18*4 + 7.5*9 + 45*4 = 72 + 67.5 + 180 = 319.5 -> 320
        Assert.Equal(18, context.Proteins);
        Assert.Equal(7.5m, context.Fats);
        Assert.Equal(45, context.Carbs);
        Assert.Equal(320, context.Calories);
        Assert.Equal("150 г", context.ServingSize);
        Assert.Equal(ConversationState.Idle, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleServingGramsAsync_WithLiterServing_ScalesMacrosByLitersAndFormatsServingSizeInLiters()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Морс";
        context.MacrosPerHundredGrams = true;
        context.IsLiterServing = true;
        context.Proteins = 0.2m;
        context.Fats = 0m;
        context.Carbs = 10m;
        context.State = ConversationState.AwaitingMealServingGrams;

        await h.Scenario.HandleServingGramsAsync(ChatId, UserId, "0.5", CancellationToken.None);

        // На 0.5 л: Б 0.1, Ж 0, У 5.
        Assert.Equal(0.1m, context.Proteins);
        Assert.Equal(0m, context.Fats);
        Assert.Equal(5m, context.Carbs);
        Assert.Equal("0.5 л", context.ServingSize);
        Assert.Equal(ConversationState.Idle, context.State);
    }

    [Fact]
    public async Task HandleServingGramsAsync_WithLiterServing_RejectsPlainGramsFormatOutOfLiterRange()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Морс";
        context.IsLiterServing = true;
        context.State = ConversationState.AwaitingMealServingGrams;

        await h.Scenario.HandleServingGramsAsync(ChatId, UserId, "150", CancellationToken.None); // 150 л — явно за пределами разумного

        Assert.Equal(ConversationState.AwaitingMealServingGrams, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleServingGramsAsync_WithInvalidWeight_SendsValidationErrorAndKeepsWaiting()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.ProductName = "Гречка";
        context.MacrosPerHundredGrams = true;
        context.State = ConversationState.AwaitingMealServingGrams;

        await h.Scenario.HandleServingGramsAsync(ChatId, UserId, "не число", CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingMealServingGrams, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleNameAsync_WithInvalidName_SendsValidationErrorAndKeepsWaitingForName()
    {
        var h = CreateHarness();
        h.States.Get(UserId).State = ConversationState.AwaitingMealProductName;

        await h.Scenario.HandleNameAsync(ChatId, UserId, "a", CancellationToken.None); // короче минимума

        var context = h.States.Get(UserId);
        Assert.Null(context.ProductName);
        Assert.Equal(ConversationState.AwaitingMealProductName, context.State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleMacrosAsync_WithValidMacros_ComputesCaloriesAndOffersToSaveFavorite()
    {
        var h = CreateHarness();
        h.States.Get(UserId).ProductName = "Гречка";
        h.States.Get(UserId).State = ConversationState.AwaitingMealProductMacros;

        await h.Scenario.HandleMacrosAsync(ChatId, UserId, "12 5 30", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal(213, context.Calories); // 12*4 + 5*9 + 30*4
        Assert.Equal(ConversationState.Idle, context.State);
        Assert.NotNull(context.ActiveInlineMessageId);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleMacrosAsync_WithInvalidMacros_SendsValidationErrorAndDoesNotAdvanceState()
    {
        var h = CreateHarness();
        h.States.Get(UserId).ProductName = "Гречка";
        h.States.Get(UserId).State = ConversationState.AwaitingMealProductMacros;

        await h.Scenario.HandleMacrosAsync(ChatId, UserId, "не число", CancellationToken.None);

        Assert.Equal(ConversationState.AwaitingMealProductMacros, h.States.Get(UserId).State);
        Assert.Single(h.Bot.Sent);
    }

    [Fact]
    public async Task HandleFavoritePickedAsync_WithKnownFavorite_LoadsDraftAndAsksForMealType()
    {
        var h = CreateHarness();
        var favorite = new FavoriteProduct { Id = 3, UserId = UserId, Name = "Рис", Calories = 300, Proteins = 7, Fats = 1, Carbs = 78 };
        h.Favorites.Setup(f => f.GetAsync(UserId, 3, It.IsAny<CancellationToken>())).ReturnsAsync(favorite);

        var query = BuildCallbackQuery(data: "pf:3");

        await h.Scenario.HandleFavoritePickedAsync(query, argument: "3", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.Equal("Рис", context.ProductName);
        Assert.Equal(3, context.FavoriteProductId);
        Assert.Single(h.Bot.Edited);
        Assert.Single(h.Bot.AnsweredCallbackIds);
    }

    [Fact]
    public async Task HandleFavoritePickedAsync_WithWaterFavorite_AsksForLitersInsteadOfGrams()
    {
        var h = CreateHarness();
        var water = new FavoriteProduct
        {
            Id = 4, UserId = UserId, Name = "Вода", Calories = 0, Proteins = 0, Fats = 0, Carbs = 0,
            IsFixedServing = false, CategoryKind = FavoriteCategoryKind.Water
        };
        h.Favorites.Setup(f => f.GetAsync(UserId, 4, It.IsAny<CancellationToken>())).ReturnsAsync(water);

        var query = BuildCallbackQuery(data: "pf:4");
        await h.Scenario.HandleFavoritePickedAsync(query, argument: "4", CancellationToken.None);

        var context = h.States.Get(UserId);
        Assert.True(context.IsLiterServing);
        Assert.Equal(ConversationState.AwaitingMealServingGrams, context.State);
        Assert.Contains("литр", h.Bot.Edited[0].Text);
    }

    [Fact]
    public async Task HandleFavoritePickedAsync_WithUnknownFavorite_AnswersWithAlertAndDoesNotEditMessage()
    {
        var h = CreateHarness();
        h.Favorites.Setup(f => f.GetAsync(UserId, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FavoriteProduct?)null);

        var query = BuildCallbackQuery(data: "pf:99");

        await h.Scenario.HandleFavoritePickedAsync(query, argument: "99", CancellationToken.None);

        Assert.Empty(h.Bot.Edited);
        Assert.Single(h.Bot.AnsweredCallbackIds);
    }

    [Fact]
    public async Task HandleMealTypeAsync_LogsEntry_ShowsUpdatedProgress_AndResetsDialog()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Гречка", 12, 5, 30));

        var loggedEntry = new FoodLogEntry { Id = 10, UserId = UserId, ProductName = "Гречка", Calories = 213, MealType = MealType.Lunch };
        h.FoodLog.Setup(f => f.LogAsync(UserId, It.IsAny<ProductDraft>(), MealType.Lunch, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loggedEntry);
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProgress() with { ConsumedCalories = 213 });

        var query = BuildCallbackQuery(data: "mt:2");

        await h.Scenario.HandleMealTypeAsync(query, argument: "2", CancellationToken.None);

        h.FoodLog.Verify(f => f.LogAsync(UserId, It.IsAny<ProductDraft>(), MealType.Lunch, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Null(h.States.Get(UserId).ProductName);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleMealTypeAsync_WithStaleDialog_DoesNotCallFoodLog()
    {
        var h = CreateHarness(); // контекст пуст — ProductName не задан

        var query = BuildCallbackQuery(data: "mt:2");

        await h.Scenario.HandleMealTypeAsync(query, argument: "2", CancellationToken.None);

        h.FoodLog.Verify(f => f.LogAsync(It.IsAny<long>(), It.IsAny<ProductDraft>(), It.IsAny<MealType>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(h.Bot.AnsweredCallbackIds);
    }

    [Fact]
    public async Task HandleMealTypeAsync_WithDuplicateNameInCurrentCycle_AsksForConfirmationInsteadOfLogging()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Гречка", 12, 5, 30));

        var existing = new FoodLogEntry { Id = 7, UserId = UserId, ProductName = "гречка", Calories = 200, MealType = MealType.Breakfast };
        h.FoodLog.Setup(f => f.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });

        var query = BuildCallbackQuery(data: "mt:2");

        await h.Scenario.HandleMealTypeAsync(query, argument: "2", CancellationToken.None);

        h.FoodLog.Verify(f => f.LogAsync(It.IsAny<long>(), It.IsAny<ProductDraft>(), It.IsAny<MealType>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        h.FoodLog.Verify(f => f.DeleteAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        // Диалог не завершён — продукт всё ещё в контексте, ждём ответа на предупреждение.
        Assert.Equal("Гречка", h.States.Get(UserId).ProductName);
        Assert.Single(h.Bot.Edited);
    }

    [Fact]
    public async Task HandleDuplicateMealConfirmAsync_Yes_DeletesOldEntryThenLogsNew()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Гречка", 12, 5, 30));

        var loggedEntry = new FoodLogEntry { Id = 10, UserId = UserId, ProductName = "Гречка", Calories = 213, MealType = MealType.Lunch };
        h.FoodLog.Setup(f => f.LogAsync(UserId, It.IsAny<ProductDraft>(), MealType.Lunch, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loggedEntry);
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProgress() with { ConsumedCalories = 213 });

        var query = BuildCallbackQuery(data: "dmc:yes:2:7");

        await h.Scenario.HandleDuplicateMealConfirmAsync(query, argument: "yes:2:7", CancellationToken.None);

        h.FoodLog.Verify(f => f.DeleteAsync(UserId, 7, It.IsAny<CancellationToken>()), Times.Once);
        h.FoodLog.Verify(f => f.LogAsync(UserId, It.IsAny<ProductDraft>(), MealType.Lunch, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ConversationState.Idle, h.States.Get(UserId).State);
        Assert.Null(h.States.Get(UserId).ProductName);
    }

    [Fact]
    public async Task HandleDuplicateMealConfirmAsync_No_LogsNewEntryWithoutDeletingOld()
    {
        var h = CreateHarness();
        var context = h.States.Get(UserId);
        context.Apply(ProductDraft.FromMacros("Гречка", 12, 5, 30));

        var loggedEntry = new FoodLogEntry { Id = 11, UserId = UserId, ProductName = "Гречка", Calories = 213, MealType = MealType.Lunch };
        h.FoodLog.Setup(f => f.LogAsync(UserId, It.IsAny<ProductDraft>(), MealType.Lunch, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loggedEntry);
        h.Progress.Setup(p => p.GetCurrentCycleAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProgress() with { ConsumedCalories = 213 });

        var query = BuildCallbackQuery(data: "dmc:no:2:7");

        await h.Scenario.HandleDuplicateMealConfirmAsync(query, argument: "no:2:7", CancellationToken.None);

        h.FoodLog.Verify(f => f.DeleteAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.FoodLog.Verify(f => f.LogAsync(UserId, It.IsAny<ProductDraft>(), MealType.Lunch, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDuplicateMealConfirmAsync_WithStaleDialog_DoesNotLogOrDelete()
    {
        var h = CreateHarness(); // контекст пуст — ProductName не задан

        var query = BuildCallbackQuery(data: "dmc:yes:2:7");

        await h.Scenario.HandleDuplicateMealConfirmAsync(query, argument: "yes:2:7", CancellationToken.None);

        h.FoodLog.Verify(f => f.DeleteAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.FoodLog.Verify(f => f.LogAsync(It.IsAny<long>(), It.IsAny<ProductDraft>(), It.IsAny<MealType>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(h.Bot.AnsweredCallbackIds);
    }

    private static CallbackQuery BuildCallbackQuery(string data) => new()
    {
        Id = "cb-1",
        From = new User { Id = UserId, FirstName = "Test" },
        Message = new Message { Id = 55, Chat = new Chat { Id = ChatId } },
        Data = data
    };
}

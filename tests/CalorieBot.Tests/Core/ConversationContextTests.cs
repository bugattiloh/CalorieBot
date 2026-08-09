using CalorieBot.Core.Models;
using CalorieBot.Core.State;

namespace CalorieBot.Tests.Core;

public class ConversationContextTests
{
    [Fact]
    public void NewContext_StartsIdleWithNoDraftData()
    {
        var context = new ConversationContext();

        Assert.Equal(ConversationState.Idle, context.State);
        Assert.Null(context.ProductName);
        Assert.Null(context.FavoriteProductId);
        Assert.Null(context.ActiveInlineMessageId);
    }

    [Fact]
    public void Apply_CopiesDraftFieldsIntoContext()
    {
        var context = new ConversationContext();
        var draft = ProductDraft.FromMacros("Гречка", proteins: 12, fats: 3, carbs: 60, servingSize: "150 г");

        context.Apply(draft);

        Assert.Equal("Гречка", context.ProductName);
        Assert.Equal(12, context.Proteins);
        Assert.Equal(3, context.Fats);
        Assert.Equal(60, context.Carbs);
        Assert.Equal("150 г", context.ServingSize);
        Assert.Equal(draft.Calories, context.Calories);
    }

    [Fact]
    public void ToDraft_RoundTripsWhatWasApplied()
    {
        var context = new ConversationContext();
        var original = ProductDraft.FromMacros("Рис", proteins: 7, fats: 1, carbs: 78);

        context.Apply(original);
        var roundTripped = context.ToDraft();

        Assert.Equal(original.Name, roundTripped.Name);
        Assert.Equal(original.Proteins, roundTripped.Proteins);
        Assert.Equal(original.Fats, roundTripped.Fats);
        Assert.Equal(original.Carbs, roundTripped.Carbs);
        Assert.Equal(original.Calories, roundTripped.Calories);
    }

    [Fact]
    public void Reset_ClearsEverythingBackToIdle()
    {
        var context = new ConversationContext
        {
            State = ConversationState.AwaitingMealProductMacros,
            ProductName = "Банан",
            MacrosPerHundredGrams = true,
            Proteins = 1,
            Fats = 1,
            Carbs = 20,
            Calories = 100,
            ServingSize = "1 шт",
            FavoriteProductId = 5,
            ActiveInlineMessageId = 42
        };

        context.Reset();

        Assert.Equal(ConversationState.Idle, context.State);
        Assert.Null(context.ProductName);
        Assert.False(context.MacrosPerHundredGrams);
        Assert.Equal(0, context.Proteins);
        Assert.Equal(0, context.Fats);
        Assert.Equal(0, context.Carbs);
        Assert.Equal(0, context.Calories);
        Assert.Null(context.ServingSize);
        Assert.Null(context.FavoriteProductId);
        Assert.Null(context.ActiveInlineMessageId);
    }
}

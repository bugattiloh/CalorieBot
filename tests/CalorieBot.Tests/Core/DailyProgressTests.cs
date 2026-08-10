using CalorieBot.Core.Models;
using CalorieBot.Data.Entities;

namespace CalorieBot.Tests.Core;

public class DailyProgressTests
{
    private static DailyProgress Build(int limit, int consumed) => new()
    {
        CycleStartedAt = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
        TrackingMode = CalorieTrackingMode.Calories,
        CalorieLimit = limit,
        ConsumedCalories = consumed
    };

    private static FavoriteProduct Product(int calories, decimal proteins = 0, decimal fats = 0, decimal carbs = 0, bool isFixedServing = true) => new()
    {
        Name = "Тест",
        Calories = calories,
        Proteins = proteins,
        Fats = fats,
        Carbs = carbs,
        IsFixedServing = isFixedServing
    };

    [Fact]
    public void RemainingCalories_IsLimitMinusConsumed_WhenUnderLimit()
    {
        var progress = Build(limit: 2000, consumed: 1200);

        Assert.Equal(800, progress.RemainingCalories);
        Assert.False(progress.IsExceeded);
        Assert.Equal(0, progress.ExceededBy);
    }

    [Fact]
    public void RemainingCalories_NeverGoesNegative_WhenOverLimit()
    {
        var progress = Build(limit: 2000, consumed: 2500);

        Assert.Equal(0, progress.RemainingCalories);
        Assert.True(progress.IsExceeded);
        Assert.Equal(500, progress.ExceededBy);
    }

    [Theory]
    [InlineData(2000, 1000, 50)]
    [InlineData(2000, 2000, 100)]
    [InlineData(2000, 2500, 125)]
    [InlineData(2000, 0, 0)]
    public void PercentUsed_ComputesRoundedPercentage(int limit, int consumed, int expectedPercent)
    {
        var progress = Build(limit, consumed);

        Assert.Equal(expectedPercent, progress.PercentUsed);
    }

    [Fact]
    public void PercentUsed_IsZero_WhenLimitIsZeroOrLess()
    {
        var progress = Build(limit: 0, consumed: 100);

        Assert.Equal(0, progress.PercentUsed);
    }

    [Fact]
    public void IsExceeded_IsFalse_WhenConsumedEqualsLimit()
    {
        var progress = Build(limit: 2000, consumed: 2000);

        Assert.False(progress.IsExceeded);
        Assert.Equal(0, progress.RemainingCalories);
    }

    [Fact]
    public void ProteinsRemaining_IsZero_WhenNoLimitSet()
    {
        var progress = Build(limit: 2000, consumed: 500) with { Proteins = 10, ProteinsLimit = null };

        Assert.Equal(0, progress.ProteinsRemaining);
        Assert.False(progress.IsProteinsExceeded);
    }

    [Fact]
    public void ProteinsExceededBy_ReflectsOverage_WhenLimitSet()
    {
        var progress = Build(limit: 2000, consumed: 500) with { Proteins = 130, ProteinsLimit = 100 };

        Assert.True(progress.IsProteinsExceeded);
        Assert.Equal(30, progress.ProteinsExceededBy);
        Assert.Equal(0, progress.ProteinsRemaining);
    }

    [Fact]
    public void Fits_CaloriesMode_ComparesAgainstRemainingCalories()
    {
        var progress = Build(limit: 2000, consumed: 1800); // остаток 200 ккал

        Assert.True(progress.Fits(Product(calories: 150)));
        Assert.False(progress.Fits(Product(calories: 250)));
    }

    [Fact]
    public void Fits_MacrosMode_RequiresAllThreeNutrientsToFitWithTolerance()
    {
        var progress = Build(limit: 2000, consumed: 0) with
        {
            TrackingMode = CalorieTrackingMode.Macros,
            Proteins = 0, Fats = 0, Carbs = 0,
            ProteinsLimit = 100, FatsLimit = 50, CarbsLimit = 200
        };

        Assert.True(progress.Fits(Product(calories: 999, proteins: 100, fats: 50, carbs: 200))); // ровно в лимит
        Assert.False(progress.Fits(Product(calories: 10, proteins: 200, fats: 0, carbs: 0))); // белков слишком много
    }

    [Fact]
    public void Fits_MacrosMode_AlwaysTrueForFloatingServing()
    {
        var progress = Build(limit: 2000, consumed: 2000) with
        {
            TrackingMode = CalorieTrackingMode.Macros,
            ProteinsLimit = 0, FatsLimit = 0, CarbsLimit = 0
        };

        // Вес неизвестен заранее, поэтому сравнивать не с чем — считается, что подходит всегда.
        Assert.True(progress.Fits(Product(calories: 500, proteins: 200, fats: 200, carbs: 200, isFixedServing: false)));
    }
}

using CalorieBot.Core.Models;

namespace CalorieBot.Tests.Core;

public class DailyProgressTests
{
    private static DailyProgress Build(int limit, int consumed) => new()
    {
        CycleStartedAt = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
        CalorieLimit = limit,
        ConsumedCalories = consumed
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
}

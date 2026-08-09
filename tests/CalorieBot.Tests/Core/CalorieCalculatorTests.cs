using CalorieBot.Core.Nutrition;

namespace CalorieBot.Tests.Core;

public class CalorieCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(12, 5, 30, 213)] // 12*4 + 5*9 + 30*4 = 48 + 45 + 120 = 213
    [InlineData(20, 10, 0, 170)] // 20*4 + 10*9 = 80 + 90 = 170
    public void FromMacros_ComputesStandardFormula(decimal proteins, decimal fats, decimal carbs, int expected)
    {
        var result = CalorieCalculator.FromMacros(proteins, fats, carbs);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromMacros_RoundsAwayFromZero()
    {
        // 1.1*4 = 4.4 -> округляется до 4
        var result = CalorieCalculator.FromMacros(1.1m, 0m, 0m);

        Assert.Equal(4, result);
    }

    [Fact]
    public void SplitLimitToMacros_UsesThirtyThirtyFortySplit()
    {
        var (proteins, fats, carbs) = CalorieCalculator.SplitLimitToMacros(2000);

        // 2000 * 0.30 / 4 = 150; 2000 * 0.30 / 9 = 66.7; 2000 * 0.40 / 4 = 200
        Assert.Equal(150.0m, proteins);
        Assert.Equal(66.7m, fats);
        Assert.Equal(200.0m, carbs);
    }

    [Fact]
    public void SplitLimitToMacros_ReconstructedCaloriesAreCloseToLimit()
    {
        var (proteins, fats, carbs) = CalorieCalculator.SplitLimitToMacros(1800);

        var reconstructed = CalorieCalculator.FromMacros(proteins, fats, carbs);

        Assert.InRange(reconstructed, 1790, 1810);
    }
}

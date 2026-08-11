using CalorieBot.Core.Nutrition;

namespace CalorieBot.Tests.Core;

public class GoalCalculatorTests
{
    [Fact]
    public void Calculate_Female_ComputesBmrByMifflinStJeorFormula()
    {
        // Женщина 30 лет, 165 см, 65 кг: BMR = 10*65 + 6.25*165 - 5*30 - 161 = 1370.25,
        // округление до 1 знака — «от нуля», поэтому ровно 1370.25 уходит в 1370.3.
        var result = GoalCalculator.Calculate(BodySex.Female, age: 30, heightCm: 165, weightKg: 65m, ActivityLevel.Moderate, WeightGoal.Maintain);

        Assert.Equal(1370.3m, result.Bmr);
    }

    [Fact]
    public void Calculate_Male_ComputesBmrByMifflinStJeorFormula()
    {
        // Мужчина 30 лет, 180 см, 80 кг: BMR = 10*80 + 6.25*180 - 5*30 + 5 = 1780
        var result = GoalCalculator.Calculate(BodySex.Male, age: 30, heightCm: 180, weightKg: 80m, ActivityLevel.Sedentary, WeightGoal.Maintain);

        Assert.Equal(1780m, result.Bmr);
    }

    [Theory]
    [InlineData(ActivityLevel.Sedentary, 1.2)]
    [InlineData(ActivityLevel.Light, 1.375)]
    [InlineData(ActivityLevel.Moderate, 1.55)]
    [InlineData(ActivityLevel.High, 1.725)]
    [InlineData(ActivityLevel.Extreme, 1.9)]
    public void Calculate_AppliesActivityCoefficientToBmr(ActivityLevel activity, double coefficient)
    {
        var result = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, activity, WeightGoal.Maintain);

        var expectedTdee = Math.Round(1370.25m * (decimal)coefficient, 1);
        Assert.Equal(expectedTdee, result.Tdee);
    }

    [Fact]
    public void Calculate_Maintain_TargetCaloriesEqualsTdee()
    {
        var result = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, ActivityLevel.Moderate, WeightGoal.Maintain);

        Assert.Equal((int)Math.Round(result.Tdee, MidpointRounding.AwayFromZero), result.TargetCalories);
    }

    [Fact]
    public void Calculate_Lose_ReducesTargetCaloriesBelowTdee()
    {
        var result = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, ActivityLevel.Moderate, WeightGoal.Lose);

        Assert.True(result.TargetCalories < result.Tdee);
    }

    [Fact]
    public void Calculate_Gain_MatchesWalkthroughExample()
    {
        // Пример из методички округляет TDEE до 2123 ещё до применения +15% и получает 2441.
        // Я считаю от точного TDEE (2123.8875 = 1370.25*1.55), поэтому итог чуть точнее: 2442.
        var result = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, ActivityLevel.Moderate, WeightGoal.Gain);

        Assert.Equal(2442, result.TargetCalories);
    }

    [Fact]
    public void Calculate_Lose_UsesHigherProteinPerKgThanMaintain()
    {
        var lose = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, ActivityLevel.Moderate, WeightGoal.Lose);
        var maintain = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, ActivityLevel.Moderate, WeightGoal.Maintain);

        Assert.True(lose.Proteins > maintain.Proteins);
    }

    [Fact]
    public void Calculate_Lose_ProteinsAndFatsMatchWalkthroughExample()
    {
        // 65 кг: белки 2 г/кг = 130 г, жиры 1 г/кг = 65 г — совпадает с примером из методички.
        var result = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, ActivityLevel.Moderate, WeightGoal.Lose);

        Assert.Equal(130m, result.Proteins);
        Assert.Equal(65m, result.Fats);
    }

    [Fact]
    public void Calculate_Carbs_AreRemainderOfTargetCaloriesAfterProteinAndFat()
    {
        var result = GoalCalculator.Calculate(BodySex.Female, 30, 165, 65m, ActivityLevel.Moderate, WeightGoal.Lose);

        var proteinCalories = result.Proteins * 4;
        var fatCalories = result.Fats * 9;
        var expectedCarbs = Math.Round((result.TargetCalories - proteinCalories - fatCalories) / 4, 1);

        Assert.Equal(expectedCarbs, result.Carbs);
    }

    [Fact]
    public void Calculate_ReconstructedCaloriesAreCloseToTarget()
    {
        var result = GoalCalculator.Calculate(BodySex.Male, 25, 180, 90m, ActivityLevel.High, WeightGoal.Gain);

        var reconstructed = result.Proteins * 4 + result.Fats * 9 + result.Carbs * 4;
        Assert.InRange(reconstructed, result.TargetCalories - 5, result.TargetCalories + 5);
    }

    [Fact]
    public void Calculate_NeverReturnsNegativeCarbs()
    {
        // Экстремально малые вес/рост/возраст (нижняя граница допустимого ввода) — белки и жиры
        // от веса тела уже сами по себе съедают всю целевую калорийность и уходят в минус остатка.
        var result = GoalCalculator.Calculate(BodySex.Female, age: 100, heightCm: 100, weightKg: 20m, ActivityLevel.Sedentary, WeightGoal.Lose);

        Assert.Equal(0m, result.Carbs);
    }
}

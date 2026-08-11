namespace CalorieBot.Core.Nutrition;

/// <summary>Пол — единственное, что нужно формуле Миффлина — Сан-Жеора кроме антропометрии.</summary>
public enum BodySex
{
    Male = 0,
    Female = 1
}

/// <summary>Уровень физической активности — коэффициент для перевода базового обмена в суточную норму (TDEE).</summary>
public enum ActivityLevel
{
    /// <summary>Сидячая работа, отсутствие спорта — ×1.2.</summary>
    Sedentary = 0,

    /// <summary>Лёгкая активность, 1–2 тренировки в неделю — ×1.375.</summary>
    Light = 1,

    /// <summary>Средняя активность, 3–5 тренировок в неделю — ×1.55.</summary>
    Moderate = 2,

    /// <summary>Высокая активность, тренировки ежедневно — ×1.725.</summary>
    High = 3,

    /// <summary>Экстремальная активность, профессиональный спорт — ×1.9.</summary>
    Extreme = 4
}

/// <summary>Цель по весу — определяет и корректировку калорийности, и норму белка на кг.</summary>
public enum WeightGoal
{
    Lose = 0,
    Maintain = 1,
    Gain = 2
}

/// <summary>Итог расчёта: промежуточные величины (для наглядности пользователю) и готовые БЖУ в граммах.</summary>
public sealed record MacroGoal
{
    public required decimal Bmr { get; init; }

    public required decimal Tdee { get; init; }

    public required int TargetCalories { get; init; }

    public required decimal Proteins { get; init; }

    public required decimal Fats { get; init; }

    public required decimal Carbs { get; init; }
}

/// <summary>
/// Расчёт дневной нормы КБЖУ: базовый обмен (BMR, формула Миффлина — Сан-Жеора) → суточная норма
/// с поправкой на активность (TDEE) → корректировка под цель → раскладка на БЖУ в граммах.
/// Белки и жиры считаю от веса тела, углеводы — остаток калорийности.
/// </summary>
public static class GoalCalculator
{
    private static readonly Dictionary<ActivityLevel, decimal> ActivityCoefficients = new()
    {
        [ActivityLevel.Sedentary] = 1.2m,
        [ActivityLevel.Light] = 1.375m,
        [ActivityLevel.Moderate] = 1.55m,
        [ActivityLevel.High] = 1.725m,
        [ActivityLevel.Extreme] = 1.9m
    };

    /// <summary>
    /// Рекомендуемый диапазон коррекции при похудении/наборе — 10–20 % от TDEE.
    /// Беру середину диапазона, чтобы не спрашивать у пользователя ещё один параметр.
    /// </summary>
    private const decimal GoalAdjustment = 0.15m;

    private const decimal LoseProteinPerKg = 2.0m;
    private const decimal MaintainProteinPerKg = 1.6m;
    private const decimal GainProteinPerKg = 1.8m;

    /// <summary>Жиры считаю одинаково независимо от цели — это минимум для гормонального фона.</summary>
    private const decimal FatPerKg = 1.0m;

    public static MacroGoal Calculate(BodySex sex, int age, int heightCm, decimal weightKg, ActivityLevel activity, WeightGoal goal)
    {
        var bmr = sex == BodySex.Male
            ? 10m * weightKg + 6.25m * heightCm - 5m * age + 5m
            : 10m * weightKg + 6.25m * heightCm - 5m * age - 161m;

        var tdee = bmr * ActivityCoefficients[activity];

        var adjustment = goal switch
        {
            WeightGoal.Lose => -GoalAdjustment,
            WeightGoal.Gain => GoalAdjustment,
            _ => 0m
        };

        var targetCalories = (int)Math.Round(tdee * (1 + adjustment), MidpointRounding.AwayFromZero);

        var proteinPerKg = goal switch
        {
            WeightGoal.Lose => LoseProteinPerKg,
            WeightGoal.Gain => GainProteinPerKg,
            _ => MaintainProteinPerKg
        };

        var proteins = Math.Round(weightKg * proteinPerKg, 1);
        var fats = Math.Round(weightKg * FatPerKg, 1);

        var remainingCalories = targetCalories - proteins * CalorieCalculator.ProteinKcalPerGram - fats * CalorieCalculator.FatKcalPerGram;

        // При очень низком весе в паре с агрессивной целью остаток теоретически может уйти в минус —
        // углеводы ниже нуля не бывают, поэтому в этом случае просто обнуляю их.
        var carbs = remainingCalories > 0 ? Math.Round(remainingCalories / CalorieCalculator.CarbKcalPerGram, 1) : 0m;

        return new MacroGoal
        {
            Bmr = Math.Round(bmr, 1, MidpointRounding.AwayFromZero),
            Tdee = Math.Round(tdee, 1, MidpointRounding.AwayFromZero),
            TargetCalories = targetCalories,
            Proteins = proteins,
            Fats = fats,
            Carbs = carbs
        };
    }
}

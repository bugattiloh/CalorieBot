namespace CalorieBot.Core.Nutrition;

/// <summary>
/// Единственное место, где я считаю калории из БЖУ. Формула стандартная:
/// белки × 4 + жиры × 9 + углеводы × 4.
/// </summary>
public static class CalorieCalculator
{
    public const int ProteinKcalPerGram = 4;
    public const int FatKcalPerGram = 9;
    public const int CarbKcalPerGram = 4;

    /// <summary>Считаю калорийность порции и округляю до целых — в интерфейсе доли ккал не нужны.</summary>
    public static int FromMacros(decimal proteins, decimal fats, decimal carbs)
    {
        var raw = proteins * ProteinKcalPerGram + fats * FatKcalPerGram + carbs * CarbKcalPerGram;
        return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Раскладываю дневной лимит калорий на ориентиры по БЖУ в схеме 30 % / 30 % / 40 %.
    /// Это подсказка для пользователя, жёстко её нигде не проверяю.
    /// </summary>
    public static (decimal Proteins, decimal Fats, decimal Carbs) SplitLimitToMacros(int calorieLimit)
    {
        var proteins = Math.Round(calorieLimit * 0.30m / ProteinKcalPerGram, 1);
        var fats = Math.Round(calorieLimit * 0.30m / FatKcalPerGram, 1);
        var carbs = Math.Round(calorieLimit * 0.40m / CarbKcalPerGram, 1);
        return (proteins, fats, carbs);
    }
}

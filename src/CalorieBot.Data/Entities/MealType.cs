namespace CalorieBot.Data.Entities;

/// <summary>
/// Тип приёма пищи. Значения задаю явно, потому что они уезжают в базу как int
/// и менять их порядок после первого релиза уже нельзя.
/// </summary>
public enum MealType
{
    Breakfast = 1,
    Lunch = 2,
    Dinner = 3,
    Snack = 4
}

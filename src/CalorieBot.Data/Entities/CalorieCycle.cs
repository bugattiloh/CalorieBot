namespace CalorieBot.Data.Entities;

/// <summary>
/// Завершённый цикл подсчёта КБЖУ (таблица CalorieCycles) — снимок того, что было в <see cref="AppUser.CycleStartedAt"/>
/// на момент, когда пользователь нажал «Новый день». Записи FoodLog при закрытии цикла никуда не деваются,
/// этот снимок нужен только для быстрого показа истории прошлых циклов без пересчёта по журналу.
/// </summary>
public class CalorieCycle
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    /// <summary>Лимит калорий, действовавший в течение цикла — на момент закрытия мог уже отличаться от текущего.</summary>
    public int CalorieLimit { get; set; }

    public int ConsumedCalories { get; set; }

    public decimal Proteins { get; set; }

    public decimal Fats { get; set; }

    public decimal Carbs { get; set; }

    public int EntriesCount { get; set; }

    public AppUser? User { get; set; }
}

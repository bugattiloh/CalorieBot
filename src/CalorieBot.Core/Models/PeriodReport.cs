using CalorieBot.Data.Entities;

namespace CalorieBot.Core.Models;

/// <summary>
/// Сводка за несколько последних завершённых календарных дней (сегодняшний, ещё не закончившийся
/// день в неё не попадает) — для «Отчёта за неделю/месяц» в «Мой прогресс». Только текстовые
/// рекомендации: сам дневной лимит пользователя эта сводка никогда не меняет.
/// </summary>
public sealed record PeriodReport
{
    public required int PeriodDays { get; init; }

    public required int DaysWithData { get; init; }

    public required CalorieTrackingMode TrackingMode { get; init; }

    public required int CalorieLimit { get; init; }

    public required int AverageCalories { get; init; }

    public required int DaysOverCalorieLimit { get; init; }

    public required int DaysUnderCalorieLimit { get; init; }

    public decimal? ProteinsLimit { get; init; }

    public decimal? FatsLimit { get; init; }

    public decimal? CarbsLimit { get; init; }

    public required decimal AverageProteins { get; init; }

    public required decimal AverageFats { get; init; }

    public required decimal AverageCarbs { get; init; }

    /// <summary>Последний завершённый день с хотя бы одной записью — по нему считаю рекомендацию «выровнять».</summary>
    public DateOnly? LastDayWithData { get; init; }

    public int LastDayCalories { get; init; }

    public decimal LastDayProteins { get; init; }

    public decimal LastDayFats { get; init; }

    public decimal LastDayCarbs { get; init; }
}

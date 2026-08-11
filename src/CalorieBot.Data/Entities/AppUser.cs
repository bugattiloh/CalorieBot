namespace CalorieBot.Data.Entities;

/// <summary>
/// Пользователь бота (таблица Users). Класс называю AppUser, чтобы он не конфликтовал
/// с Telegram.Bot.Types.User в слое хендлеров.
/// </summary>
public class AppUser
{
    /// <summary>Telegram-идентификатор пользователя, он же первичный ключ — свои id я не генерирую.</summary>
    public long UserId { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    /// <summary>
    /// Что пользователь отслеживает: калории (лимиты БЖУ — справочные, посчитаны как 30/30/40 % от калорий)
    /// или сами БЖУ (лимиты заданы явно, калории — расчётные из них). Управляется через «🎯 Дневной лимит».
    /// </summary>
    public CalorieTrackingMode TrackingMode { get; set; } = CalorieTrackingMode.Calories;

    /// <summary>
    /// Дневной максимум калорий. Живёт до тех пор, пока пользователь сам его не заменит,
    /// поэтому никакого сброса этого поля по расписанию у меня нет. В режиме <see cref="CalorieTrackingMode.Macros"/>
    /// это значение — производное от заданных БЖУ, а не то, что пользователь вводил напрямую.
    /// </summary>
    public int DailyCalorieLimit { get; set; } = 2000;

    /// <summary>
    /// Ориентиры по БЖУ. В режиме <see cref="CalorieTrackingMode.Calories"/> — 30/30/40 % от лимита калорий
    /// (справочно, без собственного лимита). В режиме <see cref="CalorieTrackingMode.Macros"/> — то, что
    /// пользователь ввёл напрямую как жёсткий лимит.
    /// </summary>
    public decimal? DailyProteinsLimit { get; set; }

    public decimal? DailyFatsLimit { get; set; }

    public decimal? DailyCarbsLimit { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Когда лимит был выставлен в последний раз — показываю это в «Текущий лимит».</summary>
    public DateTime? GoalSetAt { get; set; }

    /// <summary>
    /// Начало текущего цикла подсчёта КБЖУ. Автоматического сброса по календарным суткам нет —
    /// у людей разные жизненные циклы, поэтому цикл живёт, пока пользователь сам не нажмёт «Новый день»
    /// (тогда текущий цикл уезжает в <see cref="CalorieCycle"/>, а это поле сдвигается на «сейчас»).
    /// </summary>
    public DateTime CycleStartedAt { get; set; }

    public ICollection<FavoriteProduct> FavoriteProducts { get; set; } = new List<FavoriteProduct>();

    public ICollection<FoodLogEntry> FoodLog { get; set; } = new List<FoodLogEntry>();

    public ICollection<CalorieCycle> Cycles { get; set; } = new List<CalorieCycle>();

    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}

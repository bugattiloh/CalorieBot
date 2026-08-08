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
    /// Дневной максимум калорий. Живёт до тех пор, пока пользователь сам его не заменит,
    /// поэтому никакого сброса этого поля по расписанию у меня нет.
    /// </summary>
    public int DailyCalorieLimit { get; set; } = 2000;

    /// <summary>Ориентиры по БЖУ. Пересчитываю их от лимита калорий как 30/30/40 %.</summary>
    public decimal? DailyProteinsLimit { get; set; }

    public decimal? DailyFatsLimit { get; set; }

    public decimal? DailyCarbsLimit { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Когда лимит был выставлен в последний раз — показываю это в «Текущий лимит».</summary>
    public DateTime? GoalSetAt { get; set; }

    public ICollection<FavoriteProduct> FavoriteProducts { get; set; } = new List<FavoriteProduct>();

    public ICollection<FoodLogEntry> FoodLog { get; set; } = new List<FoodLogEntry>();
}

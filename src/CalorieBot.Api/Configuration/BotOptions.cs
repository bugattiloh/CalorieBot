using System.ComponentModel.DataAnnotations;

namespace CalorieBot.Api.Configuration;

/// <summary>
/// Настройки бота. Валидирую их на старте, чтобы контейнер падал сразу,
/// а не через минуту работы с пустым токеном.
/// </summary>
public sealed class BotOptions
{
    public const string SectionName = "Bot";

    /// <summary>Токен из BotFather. В докере прокидываю через переменную окружения Bot__Token.</summary>
    [Required(ErrorMessage = "Не задан токен бота (Bot__Token).")]
    [MinLength(30, ErrorMessage = "Токен бота выглядит слишком коротким — проверьте значение Bot__Token.")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Сбрасывать ли апдейты, накопившиеся пока бот лежал.
    /// По умолчанию сбрасываю: отвечать на сообщения часовой давности смысла нет.
    /// </summary>
    public bool DropPendingUpdates { get; set; } = true;
}

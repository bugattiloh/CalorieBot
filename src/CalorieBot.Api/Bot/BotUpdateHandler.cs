using CalorieBot.Api.Bot.UI;
using CalorieBot.Api.Infrastructure;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CalorieBot.Api.Bot;

/// <summary>
/// Глобальный обработчик апдейтов: создаёт scope под каждый апдейт и ловит все исключения,
/// чтобы одна ошибка не уронила поллинг.
/// </summary>
public sealed class BotUpdateHandler : IUpdateHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BotStatus _status;
    private readonly ILogger<BotUpdateHandler> _logger;

    public BotUpdateHandler(
        IServiceScopeFactory scopeFactory,
        BotStatus status,
        ILogger<BotUpdateHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _status = status;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        _status.MarkUpdateHandled();

        // Свой scope: внутри живут DbContext и все сервисы этого апдейта.
        using var scope = _scopeFactory.CreateScope();

        // Скоупы логов уезжают в JSON-поля — по ним потом удобно искать проблему конкретного пользователя.
        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["UpdateId"] = update.Id,
            ["UpdateType"] = update.Type.ToString(),
            ["UserId"] = ExtractUserId(update)
        });

        try
        {
            var router = scope.ServiceProvider.GetRequiredService<UpdateRouter>();
            await router.RouteAsync(update, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Штатная остановка приложения — это не ошибка.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не смог обработать апдейт {UpdateId}", update.Id);
            await TryNotifyUserAsync(botClient, update, cancellationToken);
        }
    }

    public Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        _status.MarkError(exception);

        switch (exception)
        {
            case ApiRequestException apiException:
                _logger.LogError(
                    apiException,
                    "Telegram API вернул ошибку {ErrorCode} (источник {Source})",
                    apiException.ErrorCode, source);
                break;

            default:
                _logger.LogError(exception, "Ошибка поллинга (источник {Source})", source);
                break;
        }

        // Если Telegram недоступен, не бомблю его запросами — выжидаю паузу.
        return source == HandleErrorSource.PollingError
            ? Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>Стараюсь предупредить пользователя, что его действие не выполнилось.</summary>
    private async Task TryNotifyUserAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;

        if (chatId is null)
        {
            return;
        }

        try
        {
            if (update.CallbackQuery is not null)
            {
                // Иначе на кнопке навсегда останутся «часики».
                await botClient.AnswerCallbackQuery(update.CallbackQuery.Id, cancellationToken: ct);
            }

            await botClient.SendMessage(
                chatId.Value,
                Texts.SomethingWentWrong,
                parseMode: ParseMode.Html,
                replyMarkup: Keyboards.MainMenu,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не смог сообщить пользователю об ошибке в чат {ChatId}", chatId);
        }
    }

    private static long? ExtractUserId(Update update) =>
        update.Message?.From?.Id ?? update.CallbackQuery?.From.Id;
}

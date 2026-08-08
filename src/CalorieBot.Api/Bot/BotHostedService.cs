using CalorieBot.Api.Configuration;
using CalorieBot.Api.Infrastructure;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CalorieBot.Api.Bot;

/// <summary>
/// Фоновая служба поллинга. ReceiveAsync сам завершится по CancellationToken,
/// поэтому остановка контейнера проходит без обрывов на середине обработки апдейта.
/// </summary>
public sealed class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _client;
    private readonly IUpdateHandler _updateHandler;
    private readonly BotOptions _options;
    private readonly BotStatus _status;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(
        ITelegramBotClient client,
        IUpdateHandler updateHandler,
        IOptions<BotOptions> options,
        BotStatus status,
        IHostApplicationLifetime lifetime,
        ILogger<BotHostedService> logger)
    {
        _client = client;
        _updateHandler = updateHandler;
        _options = options.Value;
        _status = status;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var me = await _client.GetMe(stoppingToken);
            _status.MarkStarted(me.Username);
            _logger.LogInformation("Бот @{BotUsername} (id {BotId}) запущен", me.Username, me.Id);

            // Единственная команда в меню Telegram — остальное живёт на кнопках.
            await _client.SetMyCommands(
                new[] { new BotCommand { Command = "start", Description = "Запустить бота и открыть меню" } },
                cancellationToken: stoppingToken);

            var receiverOptions = new ReceiverOptions
            {
                // Забираю только то, что действительно обрабатываю.
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
                DropPendingUpdates = _options.DropPendingUpdates
            };

            await _client.ReceiveAsync(_updateHandler, receiverOptions, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Останавливаю поллинг по сигналу завершения");
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 401)
        {
            // С неверным токеном работать невозможно — падаю сразу, чтобы это было видно в логах.
            _logger.LogCritical(ex, "Telegram отклонил токен бота — проверьте Bot__Token");
            _lifetime.StopApplication();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Поллинг остановлен из-за необработанной ошибки");
            _lifetime.StopApplication();
        }
        finally
        {
            _status.MarkStopped();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Бот завершает работу, обработано апдейтов: {HandledUpdates}", _status.HandledUpdates);
        await base.StopAsync(cancellationToken);
    }
}

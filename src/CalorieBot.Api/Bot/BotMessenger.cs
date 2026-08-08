using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CalorieBot.Api.Bot;

/// <summary>
/// Тонкая обёртка над клиентом Telegram: всегда HTML-разметка и мягкая реакция на
/// «ожидаемые» ошибки API (устаревший callback, неизменённое сообщение).
/// </summary>
public sealed class BotMessenger
{
    private readonly ITelegramBotClient _client;
    private readonly ILogger<BotMessenger> _logger;

    public BotMessenger(ITelegramBotClient client, ILogger<BotMessenger> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>Отправляю сообщение с любой клавиатурой.</summary>
    public Task<Message> SendAsync(
        long chatId,
        string text,
        IReplyMarkup? replyMarkup = null,
        CancellationToken ct = default) =>
        _client.SendMessage(
            chatId,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: replyMarkup,
            cancellationToken: ct);

    /// <summary>
    /// Переписываю уже отправленное сообщение — так пошаговые диалоги не засоряют чат.
    /// «message is not modified» игнорирую: пользователь просто нажал ту же кнопку дважды.
    /// </summary>
    public async Task<Message?> EditAsync(
        long chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken ct = default)
    {
        try
        {
            return await _client.EditMessageText(
                chatId,
                messageId,
                text,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message to edit not found", StringComparison.OrdinalIgnoreCase))
        {
            // Сообщение удалили — отправляю новое, чтобы диалог не оборвался.
            _logger.LogDebug("Сообщение {MessageId} в чате {ChatId} уже удалено, отправляю новое", messageId, chatId);
            return await SendAsync(chatId, text, replyMarkup, ct);
        }
    }

    /// <summary>
    /// Убираю инлайн-кнопки у сообщения: делаю это, когда сценарий закончился,
    /// чтобы старые кнопки не «стреляли» повторно.
    /// </summary>
    public async Task RemoveKeyboardAsync(long chatId, int messageId, CancellationToken ct = default)
    {
        try
        {
            await _client.EditMessageReplyMarkup(chatId, messageId, InlineKeyboardMarkup.Empty(), cancellationToken: ct);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogDebug(ex, "Не смог убрать клавиатуру у сообщения {MessageId} в чате {ChatId}", messageId, chatId);
        }
    }

    /// <summary>Закрываю «часики» на инлайн-кнопке. Просроченный запрос — не ошибка.</summary>
    public async Task AnswerAsync(
        CallbackQuery query,
        string? text = null,
        bool showAlert = false,
        CancellationToken ct = default)
    {
        try
        {
            await _client.AnswerCallbackQuery(query.Id, text, showAlert: showAlert, cancellationToken: ct);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogDebug(ex, "Не смог ответить на callback {CallbackId} — вероятно, он устарел", query.Id);
        }
    }
}

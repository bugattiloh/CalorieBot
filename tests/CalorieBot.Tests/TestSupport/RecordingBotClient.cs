using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CalorieBot.Tests.TestSupport;

public sealed record RecordedMessage(long ChatId, int MessageId, string Text, IReplyMarkup? ReplyMarkup);

/// <summary>
/// Заменяет реальный Telegram-клиент в тестах: перехватывает вызовы <see cref="ITelegramBotClient.SendRequest{TResponse}"/>,
/// которые делают методы-расширения SendMessage/EditMessageText/AnswerCallbackQuery, и запоминает, что было отправлено,
/// вместо похода в реальный Bot API.
/// </summary>
public sealed class RecordingBotClient
{
    private int _nextMessageId = 1;

    public Mock<ITelegramBotClient> Mock { get; } = new();

    public List<RecordedMessage> Sent { get; } = new();

    public List<RecordedMessage> Edited { get; } = new();

    public List<string> AnsweredCallbackIds { get; } = new();

    public RecordingBotClient()
    {
        Mock.Setup(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<Message> request, CancellationToken _) => HandleMessageRequest(request));

        Mock.Setup(c => c.SendRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<bool> request, CancellationToken _) => HandleBoolRequest(request));
    }

    public ITelegramBotClient Client => Mock.Object;

    private Message HandleMessageRequest(IRequest<Message> request)
    {
        switch (request)
        {
            case SendMessageRequest send:
            {
                var chatId = send.ChatId.Identifier ?? 0;
                var messageId = _nextMessageId++;
                Sent.Add(new RecordedMessage(chatId, messageId, send.Text, send.ReplyMarkup));
                return new Message { Id = messageId, Chat = new Chat { Id = chatId }, Text = send.Text };
            }

            case EditMessageTextRequest edit:
            {
                var chatId = edit.ChatId.Identifier ?? 0;
                Edited.Add(new RecordedMessage(chatId, edit.MessageId, edit.Text, edit.ReplyMarkup));
                return new Message { Id = edit.MessageId, Chat = new Chat { Id = chatId }, Text = edit.Text };
            }

            case EditMessageReplyMarkupRequest markup:
            {
                var chatId = markup.ChatId.Identifier ?? 0;
                return new Message { Id = markup.MessageId, Chat = new Chat { Id = chatId } };
            }

            default:
                throw new NotSupportedException($"RecordingBotClient не умеет обрабатывать {request.GetType().Name}");
        }
    }

    private bool HandleBoolRequest(IRequest<bool> request)
    {
        if (request is AnswerCallbackQueryRequest answer)
        {
            AnsweredCallbackIds.Add(answer.CallbackQueryId);
            return true;
        }

        throw new NotSupportedException($"RecordingBotClient не умеет обрабатывать {request.GetType().Name}");
    }
}

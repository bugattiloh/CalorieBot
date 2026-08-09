using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Time;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «🆕 Новый день»: ручное закрытие текущего цикла подсчёта КБЖУ (без привязки к календарным
/// суткам — у людей разные жизненные ритмы) и просмотр истории прошлых циклов.
/// </summary>
public sealed class CycleScenario
{
    private readonly BotMessenger _messenger;
    private readonly ICycleService _cycles;
    private readonly IProgressService _progress;
    private readonly IConversationStateStore _states;
    private readonly IDayClock _clock;

    public CycleScenario(
        BotMessenger messenger,
        ICycleService cycles,
        IProgressService progress,
        IConversationStateStore states,
        IDayClock clock)
    {
        _messenger = messenger;
        _cycles = cycles;
        _progress = progress;
        _states = states;
        _clock = clock;
    }

    /// <summary>Показываю подтверждение перед закрытием текущего цикла — случайный тап не должен его сбрасывать.</summary>
    public async Task ShowNewDayConfirmAsync(long chatId, long userId, CancellationToken ct)
    {
        _states.Get(userId).Reset();

        var progress = await _progress.GetCurrentCycleAsync(userId, ct);
        await _messenger.SendAsync(chatId, Texts.AskNewDayConfirm(progress, _clock.Offset), Keyboards.NewDayConfirm, ct);
    }

    /// <summary>Обрабатываю ответ на подтверждение начала нового дня.</summary>
    public async Task HandleNewDayConfirmAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        if (argument != "yes")
        {
            await _messenger.AnswerAsync(query, ct: ct);
            await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, "Отменено — текущий цикл продолжается.", replyMarkup: null, ct);
            return;
        }

        var closedCycle = await _cycles.StartNewCycleAsync(query.From.Id, ct);

        await _messenger.AnswerAsync(query, "Новый день начат 🆕", ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.NewDayStarted(closedCycle), replyMarkup: null, ct);
    }

    /// <summary>Показываю страницу истории прошлых циклов.</summary>
    public async Task ShowHistoryAsync(long chatId, long userId, int page, int? editMessageId, CancellationToken ct)
    {
        var totalCount = await _cycles.GetHistoryCountAsync(userId, ct);

        if (totalCount == 0)
        {
            await SendOrEditAsync(chatId, editMessageId, Texts.EmptyCycleHistory, Keyboards.ToMenuOnly, ct);
            return;
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)Keyboards.PageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        var cycles = await _cycles.GetHistoryAsync(userId, page * Keyboards.PageSize, Keyboards.PageSize, ct);
        var text = Texts.CycleHistoryPage(cycles, page, Keyboards.PageSize, totalCount, _clock.Offset);
        var keyboard = Keyboards.PageNavigation(page, totalPages, Callbacks.CycleHistoryPage);

        await SendOrEditAsync(chatId, editMessageId, text, keyboard, ct);
    }

    private async Task SendOrEditAsync(long chatId, int? editMessageId, string text, InlineKeyboardMarkup keyboard, CancellationToken ct)
    {
        if (editMessageId is null)
        {
            await _messenger.SendAsync(chatId, text, keyboard, ct);
        }
        else
        {
            await _messenger.EditAsync(chatId, editMessageId.Value, text, keyboard, ct);
        }
    }
}

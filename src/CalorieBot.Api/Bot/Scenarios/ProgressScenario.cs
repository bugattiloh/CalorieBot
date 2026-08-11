using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Time;
using Telegram.Bot.Types;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «📊 Мой прогресс»: прогресс текущего цикла и сразу его история одним сообщением.
/// Экран только читает данные, поэтому состояние диалога здесь сбрасываю.
/// </summary>
public sealed class ProgressScenario
{
    private readonly BotMessenger _messenger;
    private readonly IProgressService _progress;
    private readonly IFoodLogService _foodLog;
    private readonly IConversationStateStore _states;
    private readonly IDayClock _clock;

    public ProgressScenario(
        BotMessenger messenger,
        IProgressService progress,
        IFoodLogService foodLog,
        IConversationStateStore states,
        IDayClock clock)
    {
        _messenger = messenger;
        _progress = progress;
        _foodLog = foodLog;
        _states = states;
        _clock = clock;
    }

    /// <summary>Главный экран: лимит/БЖУ, остаток, подходящие любимые продукты и история цикла.</summary>
    public async Task ShowProgressAsync(long chatId, long userId, CancellationToken ct)
    {
        _states.Get(userId).Reset();

        var progress = await _progress.GetCurrentCycleAsync(userId, ct);
        var entries = await _foodLog.GetCurrentCycleAsync(userId, ct);

        await _messenger.SendAsync(chatId, Texts.Progress(progress, entries, _clock.Offset), Keyboards.ProgressReportChoice, ct);
    }

    /// <summary>Отчёт за неделю/месяц по кнопке под экраном прогресса — отправляю отдельным сообщением.</summary>
    public async Task HandlePeriodReportAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var days) || (days != 7 && days != 30))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var report = await _progress.GetReportAsync(userId, days, ct);

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.SendAsync(query.Message.Chat.Id, Texts.PeriodReport(report), replyMarkup: null, ct);
    }
}

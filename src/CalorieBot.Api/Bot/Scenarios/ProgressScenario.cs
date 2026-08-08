using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Time;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарии просмотра: «📊 Мой прогресс» и «📋 История сегодня».
/// Оба экрана только читают данные, поэтому состояние диалога здесь сбрасываю.
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

    /// <summary>Главный экран: лимит, съеденное, остаток и подходящие любимые продукты.</summary>
    public async Task ShowProgressAsync(long chatId, long userId, CancellationToken ct)
    {
        _states.Get(userId).Reset();

        var progress = await _progress.GetTodayAsync(userId, ct);
        await _messenger.SendAsync(chatId, Texts.Progress(progress), Keyboards.MainMenu, ct);
    }

    /// <summary>Что съедено за сегодня, с разбивкой по приёмам пищи.</summary>
    public async Task ShowHistoryAsync(long chatId, long userId, CancellationToken ct)
    {
        _states.Get(userId).Reset();

        var entries = await _foodLog.GetTodayAsync(userId, ct);
        var progress = await _progress.GetTodayAsync(userId, ct);

        await _messenger.SendAsync(
            chatId,
            Texts.History(entries, progress, _clock.Offset),
            Keyboards.MainMenu,
            ct);
    }
}

using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Time;
using CalorieBot.Core.Validation;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «🎯 Дневной лимит»: просмотр и изменение дневного максимума калорий.
/// Лимит не сбрасывается по расписанию — он живёт до следующей явной замены.
/// </summary>
public sealed class LimitScenario
{
    private readonly BotMessenger _messenger;
    private readonly IUserService _users;
    private readonly IProgressService _progress;
    private readonly IConversationStateStore _states;
    private readonly IDayClock _clock;

    public LimitScenario(
        BotMessenger messenger,
        IUserService users,
        IProgressService progress,
        IConversationStateStore states,
        IDayClock clock)
    {
        _messenger = messenger;
        _users = users;
        _progress = progress;
        _states = states;
        _clock = clock;
    }

    /// <summary>Показываю подменю лимита.</summary>
    public async Task ShowMenuAsync(long chatId, long userId, CancellationToken ct)
    {
        _states.Get(userId).Reset();
        var user = await _users.GetAsync(userId, ct);

        await _messenger.SendAsync(
            chatId,
            $"🎯 <b>Дневной лимит</b>\n\nСейчас: <b>{user.DailyCalorieLimit} ккал</b> в день.",
            Keyboards.LimitMenu,
            ct);
    }

    /// <summary>Прошу новое значение лимита.</summary>
    public async Task StartChangeAsync(long chatId, long userId, CancellationToken ct)
    {
        var context = _states.Get(userId);
        context.Reset();
        context.State = ConversationState.AwaitingCalorieLimit;

        var user = await _users.GetAsync(userId, ct);
        await _messenger.SendAsync(chatId, Texts.AskCalorieLimit(user.DailyCalorieLimit), Keyboards.LimitMenu, ct);
    }

    /// <summary>Принимаю новый лимит, проверяю диапазон и сохраняю.</summary>
    public async Task HandleNewLimitAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseCalorieLimit(text, out var limit, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.LimitMenu, ct);
            return;
        }

        var user = await _users.UpdateCalorieLimitAsync(userId, limit, ct);
        var progress = await _progress.GetCurrentCycleAsync(userId, ct);

        _states.Get(userId).Reset();

        await _messenger.SendAsync(chatId, Texts.LimitUpdated(user, progress), Keyboards.MainMenu, ct);
    }

    /// <summary>Показываю текущий лимит и как он расходуется сегодня.</summary>
    public async Task ShowCurrentAsync(long chatId, long userId, CancellationToken ct)
    {
        var user = await _users.GetAsync(userId, ct);
        var progress = await _progress.GetCurrentCycleAsync(userId, ct);

        await _messenger.SendAsync(
            chatId,
            Texts.CurrentLimit(user, progress, _clock.Offset),
            Keyboards.LimitMenu,
            ct);
    }
}

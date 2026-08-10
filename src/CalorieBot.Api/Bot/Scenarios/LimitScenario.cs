using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Time;
using CalorieBot.Core.Validation;
using CalorieBot.Data.Entities;
using Telegram.Bot.Types;

namespace CalorieBot.Api.Bot.Scenarios;

/// <summary>
/// Сценарий «🎯 Дневной лимит»: просмотр и изменение дневного лимита — по калориям либо по БЖУ напрямую.
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

        var summary = user.TrackingMode == CalorieTrackingMode.Macros
            ? $"🎯 <b>Дневной лимит</b>\n\nСейчас: Б {Texts.Num(user.DailyProteinsLimit ?? 0m)} / Ж {Texts.Num(user.DailyFatsLimit ?? 0m)} / У {Texts.Num(user.DailyCarbsLimit ?? 0m)} г."
            : $"🎯 <b>Дневной лимит</b>\n\nСейчас: <b>{user.DailyCalorieLimit} ккал</b> в день.";

        await _messenger.SendAsync(chatId, summary, Keyboards.LimitMenu, ct);
    }

    /// <summary>Спрашиваю, что именно менять — калории или БЖУ.</summary>
    public async Task StartChangeAsync(long chatId, long userId, CancellationToken ct)
    {
        var context = _states.Get(userId);
        context.Reset();
        context.State = ConversationState.AwaitingLimitMode;

        await _messenger.SendAsync(chatId, Texts.AskLimitMode, Keyboards.LimitModeChoice, ct);
    }

    /// <summary>Пользователь выбрал режим — спрашиваю соответствующее значение.</summary>
    public async Task HandleLimitModeAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var context = _states.Get(userId);

        await _messenger.AnswerAsync(query, ct: ct);

        if (argument == "macro")
        {
            context.State = ConversationState.AwaitingMacroLimits;
            await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskMacroLimits, replyMarkup: null, ct);
            return;
        }

        context.State = ConversationState.AwaitingCalorieLimit;
        var user = await _users.GetAsync(userId, ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskCalorieLimit(user.DailyCalorieLimit), replyMarkup: null, ct);
    }

    /// <summary>Принимаю новый лимит калорий, проверяю диапазон и сохраняю.</summary>
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

    /// <summary>Принимаю новые лимиты БЖУ, проверяю и сохраняю.</summary>
    public async Task HandleNewMacroLimitsAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseMacros(text, out var proteins, out var fats, out var carbs, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.LimitMenu, ct);
            return;
        }

        var user = await _users.UpdateMacroLimitsAsync(userId, proteins, fats, carbs, ct);
        var progress = await _progress.GetCurrentCycleAsync(userId, ct);

        _states.Get(userId).Reset();

        await _messenger.SendAsync(chatId, Texts.MacroLimitsUpdated(user, progress), Keyboards.MainMenu, ct);
    }

    /// <summary>Показываю текущий лимит и как он расходуется в этом цикле.</summary>
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

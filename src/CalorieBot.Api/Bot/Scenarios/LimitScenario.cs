using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Nutrition;
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

        if (argument == "calc")
        {
            context.State = ConversationState.AwaitingCalcSex;
            await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskCalcSex, Keyboards.CalcSexChoice, ct);
            return;
        }

        context.State = ConversationState.AwaitingCalorieLimit;
        var user = await _users.GetAsync(userId, ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskCalorieLimit(user.DailyCalorieLimit), replyMarkup: null, ct);
    }

    // ------------------------------------------------------------------
    // Калькулятор нормы КБЖУ: пол → возраст → рост → вес → активность → цель → результат
    // ------------------------------------------------------------------

    /// <summary>Пользователь выбрал пол — спрашиваю возраст.</summary>
    public async Task HandleCalcSexAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        if (argument != "m" && argument != "f")
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        context.CalcSex = argument == "m" ? BodySex.Male : BodySex.Female;
        context.State = ConversationState.AwaitingCalcAge;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskCalcAge, replyMarkup: null, ct);
    }

    /// <summary>Принимаю возраст и спрашиваю рост.</summary>
    public async Task HandleCalcAgeAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseAge(text, out var age, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.LimitMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        if (context.CalcSex is null)
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.LimitMenu, ct);
            return;
        }

        context.CalcAge = age;
        context.State = ConversationState.AwaitingCalcHeight;

        await _messenger.SendAsync(chatId, Texts.AskCalcHeight, Keyboards.LimitMenu, ct);
    }

    /// <summary>Принимаю рост и спрашиваю вес.</summary>
    public async Task HandleCalcHeightAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseHeightCm(text, out var heightCm, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.LimitMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        if (context.CalcAge is null)
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.LimitMenu, ct);
            return;
        }

        context.CalcHeightCm = heightCm;
        context.State = ConversationState.AwaitingCalcWeight;

        await _messenger.SendAsync(chatId, Texts.AskCalcWeight, Keyboards.LimitMenu, ct);
    }

    /// <summary>Принимаю вес и спрашиваю уровень активности.</summary>
    public async Task HandleCalcWeightAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        if (!InputParser.TryParseWeightKg(text, out var weightKg, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.LimitMenu, ct);
            return;
        }

        var context = _states.Get(userId);
        if (context.CalcHeightCm is null)
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.LimitMenu, ct);
            return;
        }

        context.CalcWeightKg = weightKg;
        context.State = ConversationState.AwaitingCalcActivity;

        await _messenger.SendAsync(chatId, Texts.AskCalcActivity, Keyboards.CalcActivityChoice, ct);
    }

    /// <summary>Пользователь выбрал уровень активности — спрашиваю цель.</summary>
    public async Task HandleCalcActivityAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        if (!TryParseActivity(argument, out var activity))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);
        if (context.CalcWeightKg is null)
        {
            context.Reset();
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        context.CalcActivity = activity;
        context.State = ConversationState.AwaitingCalcGoal;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskCalcGoal, Keyboards.CalcGoalChoice, ct);
    }

    /// <summary>Пользователь выбрал цель — считаю итог и показываю карточку с предложением применить.</summary>
    public async Task HandleCalcGoalAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var context = _states.Get(query.From.Id);

        if (!TryParseGoal(argument, out var goal)
            || context.CalcSex is null || context.CalcAge is null || context.CalcHeightCm is null || context.CalcWeightKg is null
            || context.CalcActivity is null)
        {
            context.Reset();
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        var result = GoalCalculator.Calculate(
            context.CalcSex.Value, context.CalcAge.Value, context.CalcHeightCm.Value, context.CalcWeightKg.Value,
            context.CalcActivity.Value, goal);

        context.State = ConversationState.Idle;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id, query.Message.MessageId, Texts.CalcResultCard(result), Keyboards.CalcResultActions(argument!), ct);
    }

    /// <summary>Применяю рассчитанные БЖУ как новый лимит — пересчитываю по тем же вводным (в контексте они ещё живы).</summary>
    public async Task HandleCalcApplyAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null)
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var context = _states.Get(userId);

        if (!TryParseGoal(argument, out var goal)
            || context.CalcSex is null || context.CalcAge is null || context.CalcHeightCm is null || context.CalcWeightKg is null
            || context.CalcActivity is null)
        {
            context.Reset();
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        var result = GoalCalculator.Calculate(
            context.CalcSex.Value, context.CalcAge.Value, context.CalcHeightCm.Value, context.CalcWeightKg.Value,
            context.CalcActivity.Value, goal);

        var user = await _users.UpdateMacroLimitsAsync(userId, result.Proteins, result.Fats, result.Carbs, ct);
        var progress = await _progress.GetCurrentCycleAsync(userId, ct);

        context.Reset();

        await _messenger.AnswerAsync(query, "Применил ✅", ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.MacroLimitsUpdated(user, progress), replyMarkup: null, ct);
    }

    private static bool TryParseActivity(string? argument, out ActivityLevel activity)
    {
        switch (argument)
        {
            case "sedentary": activity = ActivityLevel.Sedentary; return true;
            case "light": activity = ActivityLevel.Light; return true;
            case "moderate": activity = ActivityLevel.Moderate; return true;
            case "high": activity = ActivityLevel.High; return true;
            case "extreme": activity = ActivityLevel.Extreme; return true;
            default: activity = default; return false;
        }
    }

    private static bool TryParseGoal(string? argument, out WeightGoal goal)
    {
        switch (argument)
        {
            case "lose": goal = WeightGoal.Lose; return true;
            case "maintain": goal = WeightGoal.Maintain; return true;
            case "gain": goal = WeightGoal.Gain; return true;
            default: goal = default; return false;
        }
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

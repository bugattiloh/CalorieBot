using CalorieBot.Api.Bot.UI;
using CalorieBot.Core.Models;
using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Time;
using CalorieBot.Core.Validation;
using CalorieBot.Data.Entities;
using Telegram.Bot.Types;

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

    /// <summary>Показываю страницу истории прошлых циклов — по 7 штук, каждый цикл кликабелен для редактирования.</summary>
    public async Task ShowHistoryAsync(long chatId, long userId, int page, int? editMessageId, CancellationToken ct)
    {
        var totalCount = await _cycles.GetHistoryCountAsync(userId, ct);

        if (totalCount == 0)
        {
            await _messenger.SendOrEditAsync(chatId, editMessageId, Texts.EmptyCycleHistory, Keyboards.ToMenuOnly, ct);
            return;
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)Keyboards.CyclePageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        var cycles = await _cycles.GetHistoryAsync(userId, page * Keyboards.CyclePageSize, Keyboards.CyclePageSize, ct);
        var text = Texts.CycleHistoryPage(cycles, page, Keyboards.CyclePageSize, totalCount, _clock.Offset);
        var keyboard = Keyboards.CycleHistoryButtons(cycles, page, Keyboards.CyclePageSize, totalPages);

        await _messenger.SendOrEditAsync(chatId, editMessageId, text, keyboard, ct);
    }

    /// <summary>Открываю карточку конкретного прошлого цикла по тапу из «Истории».</summary>
    public async Task HandleCycleDetailsAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var cycleId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await ShowCycleDetailsInternalAsync(query.Message.Chat.Id, query.From.Id, cycleId, query.Message.MessageId, ct);
    }

    /// <summary>Спрашиваю подтверждение перед удалением записи из прошлого цикла.</summary>
    public async Task HandleEntryDeleteRequestAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null || parts.Length != 2 || !int.TryParse(parts[0], out var cycleId) || !int.TryParse(parts[1], out var entryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var entries = await _cycles.GetEntriesAsync(userId, cycleId, ct);
        var entry = entries.FirstOrDefault(e => e.Id == entryId);

        if (entry is null)
        {
            await _messenger.AnswerAsync(query, "Запись не найдена — возможно, уже удалена.", showAlert: true, ct: ct);
            await ShowCycleDetailsInternalAsync(query.Message.Chat.Id, userId, cycleId, query.Message.MessageId, ct);
            return;
        }

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(
            query.Message.Chat.Id,
            query.Message.MessageId,
            Texts.ConfirmDeleteCycleEntry(entry, _clock.Offset),
            Keyboards.CycleEntryDeleteConfirm(cycleId, entryId),
            ct);
    }

    /// <summary>Удаляю запись из прошлого цикла (снимок цикла пересчитывается в сервисе) и обновляю карточку.</summary>
    public async Task HandleEntryDeleteConfirmAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null || parts.Length != 2 || !int.TryParse(parts[0], out var cycleId) || !int.TryParse(parts[1], out var entryId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var deleted = await _cycles.DeleteEntryAsync(userId, cycleId, entryId, ct);

        await _messenger.AnswerAsync(query, deleted ? "Удалил 🗑" : "Запись уже удалена.", ct: ct);
        await ShowCycleDetailsInternalAsync(query.Message.Chat.Id, userId, cycleId, query.Message.MessageId, ct);
    }

    /// <summary>Начинаю добавление записи задним числом в закрытый цикл: имя → БЖУ → тип приёма пищи.</summary>
    public async Task StartAddEntryAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        if (query.Message is null || !int.TryParse(argument, out var cycleId))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var cycle = await _cycles.GetAsync(userId, cycleId, ct);

        if (cycle is null)
        {
            await _messenger.AnswerAsync(query, "Цикл не найден.", showAlert: true, ct: ct);
            return;
        }

        var context = _states.Get(userId);
        context.Reset();
        context.EditingCycleId = cycleId;
        context.State = ConversationState.AwaitingCycleEntryName;
        context.ActiveInlineMessageId = query.Message.MessageId;

        await _messenger.AnswerAsync(query, ct: ct);
        await _messenger.EditAsync(query.Message.Chat.Id, query.Message.MessageId, Texts.AskProductName, replyMarkup: null, ct);
    }

    /// <summary>Принимаю название продукта, который добавляем задним числом.</summary>
    public async Task HandleEntryNameAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        var context = _states.Get(userId);

        if (context.EditingCycleId is null)
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.MainMenu, ct);
            return;
        }

        if (!InputParser.TryParseProductName(text, out var name, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.MainMenu, ct);
            return;
        }

        context.ProductName = name;
        context.State = ConversationState.AwaitingCycleEntryMacros;

        await _messenger.SendAsync(chatId, Texts.AskMacros, Keyboards.MainMenu, ct);
    }

    /// <summary>Принимаю БЖУ и предлагаю выбрать тип приёма пищи для записи, добавляемой задним числом.</summary>
    public async Task HandleEntryMacrosAsync(long chatId, long userId, string? text, CancellationToken ct)
    {
        var context = _states.Get(userId);

        if (context.EditingCycleId is not { } cycleId || string.IsNullOrWhiteSpace(context.ProductName))
        {
            context.Reset();
            await _messenger.SendAsync(chatId, Texts.StaleDialog, Keyboards.MainMenu, ct);
            return;
        }

        if (!InputParser.TryParseMacros(text, out var proteins, out var fats, out var carbs, out var error))
        {
            await _messenger.SendAsync(chatId, Texts.ValidationError(error), Keyboards.MainMenu, ct);
            return;
        }

        context.Apply(ProductDraft.FromMacros(context.ProductName, proteins, fats, carbs));
        context.State = ConversationState.Idle;

        var sent = await _messenger.SendAsync(chatId, Texts.ChooseMealType(context.ToDraft()), Keyboards.CycleEntryMealTypes(cycleId), ct);
        context.ActiveInlineMessageId = sent.MessageId;
    }

    /// <summary>Финальный шаг: записываю продукт в прошлый цикл и показываю обновлённую карточку цикла.</summary>
    public async Task HandleEntryMealTypeAsync(CallbackQuery query, string? argument, CancellationToken ct)
    {
        var parts = (argument ?? string.Empty).Split(':');
        if (query.Message is null
            || parts.Length != 2
            || !int.TryParse(parts[0], out var cycleId)
            || !int.TryParse(parts[1], out var rawMealType)
            || !Enum.IsDefined(typeof(MealType), rawMealType))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, ct: ct);
            return;
        }

        var userId = query.From.Id;
        var context = _states.Get(userId);

        if (string.IsNullOrEmpty(context.ProductName))
        {
            await _messenger.AnswerAsync(query, Texts.StaleDialog, showAlert: true, ct: ct);
            return;
        }

        var cycle = await _cycles.GetAsync(userId, cycleId, ct);
        if (cycle is null)
        {
            context.Reset();
            await _messenger.AnswerAsync(query, "Цикл не найден.", showAlert: true, ct: ct);
            return;
        }

        await _cycles.AddEntryAsync(userId, cycleId, context.ToDraft(), (MealType)rawMealType, ct);
        context.Reset();

        await _messenger.AnswerAsync(query, "Добавил ✅", ct: ct);
        await ShowCycleDetailsInternalAsync(query.Message.Chat.Id, userId, cycleId, query.Message.MessageId, ct);
    }

    /// <summary>Общая точка показа карточки цикла — и по тапу из истории, и после add/delete записи.</summary>
    private async Task ShowCycleDetailsInternalAsync(long chatId, long userId, int cycleId, int? editMessageId, CancellationToken ct)
    {
        var cycle = await _cycles.GetAsync(userId, cycleId, ct);
        if (cycle is null)
        {
            await _messenger.SendOrEditAsync(chatId, editMessageId, "Цикл не найден — возможно, уже удалён.", Keyboards.ToMenuOnly, ct);
            return;
        }

        var entries = await _cycles.GetEntriesAsync(userId, cycleId, ct);
        var text = Texts.CycleDetailsCard(cycle, entries, _clock.Offset);
        var keyboard = Keyboards.CycleDetailsActions(cycleId, entries, entry => Texts.EntryButtonLabel(entry, _clock.Offset));

        await _messenger.SendOrEditAsync(chatId, editMessageId, text, keyboard, ct);
    }
}

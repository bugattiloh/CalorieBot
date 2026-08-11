using System.Globalization;
using System.Net;
using System.Text;
using CalorieBot.Core.Models;
using CalorieBot.Core.Validation;
using CalorieBot.Data.Entities;

namespace CalorieBot.Api.Bot.UI;

/// <summary>
/// Все тексты бота и их форматирование. Отправляю сообщения в режиме HTML,
/// поэтому любое пользовательское название прогоняю через <see cref="Escape"/>.
/// </summary>
public static class Texts
{
    public const string OnlyButtons =
        "Я понимаю только кнопки 🙂\nВыберите действие в меню ниже.";

    public const string StaleDialog =
        "Этот диалог уже неактуален — начните заново из меню.";

    public const string SomethingWentWrong =
        "😔 Что-то пошло не так. Я уже записал ошибку в лог — попробуйте ещё раз через меню.";

    public const string ChooseMealSource =
        "Что добавляем?\n\n💝 <b>Из любимых</b> — выбрать из сохранённых продуктов\n🔍 <b>Новый продукт</b> — ввести название и БЖУ";

    public const string FavoritesMenuText =
        "⭐ <b>Избранное</b>\n\nЗдесь я храню продукты, которые вы едите чаще всего — их можно добавлять в дневник одним нажатием.";

    public static readonly string AskProductName =
        $"Как называется продукт?\n\nОтправьте название сообщением (от {InputParser.MinNameLength} до {InputParser.MaxNameLength} символов).";

    public const string AskMacrosMode =
        "Как вам удобнее указать БЖУ?\n\n" +
        "📏 <b>На 100 г</b> — я потом спрошу вес порции и сам пересчитаю на неё.\n" +
        "🍽 <b>На порцию целиком</b> — введённые числа так и запишу.";

    /// <summary>Выбор типа порции для избранного — от него зависит, как продукт логируется в дневник.</summary>
    public const string AskFavoriteServingMode =
        "Какая у продукта порция?\n\n" +
        "🍽 <b>Фиксированная</b> (например, батончик) — КБЖУ на одну порцию, добавляется в дневник одним тапом.\n" +
        "📏 <b>На 100 г</b> (например, рис) — порция каждый раз разная, при добавлении в дневник я буду " +
        "спрашивать съеденный вес и пересчитывать КБЖУ на него.";

    /// <summary>Общая подсказка по формату — строго три числа через пробел, без минуса, максимум один знак после запятой.</summary>
    public const string MacrosFormatHint =
        "три числа через пробел, без знака «минус», не больше одного знака после запятой.";

    public const string AskMacros =
        "Теперь пришлите БЖУ <b>на порцию</b> в граммах — три числа в порядке <b>белки жиры углеводы</b>, " +
        MacrosFormatHint + "\n\n" +
        "Например: <code>12 5 1</code>\n\nКалории я посчитаю сам по формуле Б×4 + Ж×9 + У×4.";

    public const string AskMacrosPerHundred =
        "Пришлите БЖУ <b>на 100 г продукта</b> — три числа в порядке <b>белки жиры углеводы</b>, " +
        MacrosFormatHint + "\n\n" +
        "Например: <code>12 5 1</code>";

    public const string AskServingGrams =
        "Сколько грамм в порции? Отправьте число, например <code>150</code> — пересчитаю БЖУ и калории на этот вес.";

    public const string AskServingLiters =
        "Сколько литров выпито? Отправьте число, например <code>0.5</code> — пересчитаю БЖУ и калории на этот объём.";

    public const string AskServingSize =
        "Опишите порцию — например <code>200 г</code> или <code>1 стакан</code>.\n\n" +
        "Если не нужно, нажмите «⏭ Пропустить».";

    public const string EmptyFavorites =
        "⭐ Избранное пока пусто.\n\nДобавьте продукты через <b>⭐ Избранное → ➕ Добавить в избранное</b> — потом их можно будет записывать в один тап.";

    /// <summary>
    /// Приветствие при /start. Если лимит ещё не меняли, честно пишу, что он дефолтный.
    /// </summary>
    public static string Greeting(string? firstName, int calorieLimit, bool usesDefaultLimit)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "Привет" : $"Привет, {Escape(firstName)}";
        var builder = new StringBuilder();

        builder.AppendLine($"{name}! 👋");
        builder.AppendLine();
        builder.AppendLine("Я помогаю держать питание в рамках дневного максимума калорий.");
        builder.AppendLine();

        builder.AppendLine(usesDefaultLimit
            ? $"Ваш дневной лимит по умолчанию: <b>{calorieLimit} ккал</b>. Изменить его можно в «🎯 Дневной лимит»."
            : $"Ваш текущий дневной лимит: <b>{calorieLimit} ккал</b>.");

        builder.AppendLine();
        builder.AppendLine("Всё управление — кнопками ниже:");
        builder.AppendLine("🍽 записать съеденное");
        builder.AppendLine("📊 посмотреть прогресс и историю текущего цикла");
        builder.AppendLine("⭐ хранить любимые продукты");
        builder.AppendLine("🎯 менять дневной лимит (по калориям или по БЖУ)");
        builder.AppendLine("🆕 начать новый день, когда сами решите");
        builder.AppendLine();
        builder.Append("<i>Автосброса по расписанию нет — счётчик обнуляется только по кнопке «🆕 Новый день».</i>");

        return builder.ToString();
    }

    /// <summary>Карточка продукта с посчитанной калорийностью.</summary>
    public static string ProductCard(ProductDraft draft)
    {
        var serving = string.IsNullOrWhiteSpace(draft.ServingSize) ? string.Empty : $" ({Escape(draft.ServingSize!)})";

        return $"<b>{Escape(draft.Name)}</b>{serving}\n" +
               $"🔥 <b>{draft.Calories} ккал</b>\n" +
               $"Б {Num(draft.Proteins)} · Ж {Num(draft.Fats)} · У {Num(draft.Carbs)} г";
    }

    /// <summary>
    /// Главный экран: прогресс текущего цикла (по калориям либо по БЖУ — смотря что отслеживает
    /// пользователь) плюс сразу история цикла, без отдельного экрана.
    /// </summary>
    public static string Progress(DailyProgress progress, IReadOnlyList<FoodLogEntry> entries, TimeSpan offset)
    {
        var cycleStart = ToLocal(progress.CycleStartedAt, offset);

        var builder = new StringBuilder();
        builder.AppendLine("📊 <b>Мой прогресс</b>");
        builder.AppendLine();

        if (progress.TrackingMode == CalorieTrackingMode.Macros)
        {
            AppendMacroProgress(builder, progress);
        }
        else
        {
            AppendCalorieProgress(builder, progress);
        }

        builder.AppendLine();
        builder.AppendLine($"Приёмов пищи в этом цикле: <b>{progress.EntriesCount}</b>");
        builder.AppendLine($"Цикл идёт: {Duration(progress.CycleElapsed)} (с {cycleStart:dd.MM HH:mm})");
        builder.AppendLine();

        AppendFittingFavorites(builder, progress);
        builder.AppendLine();
        builder.AppendLine();
        AppendCycleHistory(builder, entries, offset);

        return builder.ToString().TrimEnd();
    }

    private static void AppendCalorieProgress(StringBuilder builder, DailyProgress progress)
    {
        builder.AppendLine($"Съедено <b>{progress.ConsumedCalories}</b> из <b>{progress.CalorieLimit}</b> ккал ({progress.PercentUsed}% от лимита).");

        if (progress.IsExceeded)
        {
            builder.AppendLine("Осталось: <b>0</b> ккал");
            builder.AppendLine(ProgressBar(progress.PercentUsed));
            builder.AppendLine();
            builder.AppendLine($"⚠️ <b>Лимит превышен на {progress.ExceededBy} ккал!</b>");
            builder.AppendLine("Не забудьте: счётчик не обнулится сам — нажмите «🆕 Новый день», когда будете готовы.");
        }
        else
        {
            builder.AppendLine($"Осталось: <b>{progress.RemainingCalories}</b> ккал");
            builder.AppendLine(ProgressBar(progress.PercentUsed));
        }

        builder.AppendLine();
        builder.AppendLine(MacroLine("🥩 Б", progress.Proteins, progress.ProteinsLimit));
        builder.AppendLine(MacroLine("🧈 Ж", progress.Fats, progress.FatsLimit));
        builder.AppendLine(MacroLine("🍞 У", progress.Carbs, progress.CarbsLimit));
    }

    private static void AppendMacroProgress(StringBuilder builder, DailyProgress progress)
    {
        builder.AppendLine($"Калорийность съеденного: <b>{progress.ConsumedCalories} ккал</b> <i>(справочно — вы отслеживаете БЖУ)</i>.");
        builder.AppendLine();
        AppendMacroBreakdown(builder, progress);

        if (progress.IsProteinsExceeded || progress.IsFatsExceeded || progress.IsCarbsExceeded)
        {
            builder.AppendLine();
            builder.Append("⚠️ Не забудьте: счётчик не обнулится сам — нажмите «🆕 Новый день», когда будете готовы.");
        }
    }

    /// <summary>Три строки по Б/Ж/У с остатком/перебором — общий блок для прогресса и подтверждения записи.</summary>
    private static void AppendMacroBreakdown(StringBuilder builder, DailyProgress progress)
    {
        AppendMacroTargetLine(builder, "🥩 Белки", progress.Proteins, progress.ProteinsLimit, progress.ProteinsRemaining, progress.IsProteinsExceeded, progress.ProteinsExceededBy);
        AppendMacroTargetLine(builder, "🧈 Жиры", progress.Fats, progress.FatsLimit, progress.FatsRemaining, progress.IsFatsExceeded, progress.FatsExceededBy);
        AppendMacroTargetLine(builder, "🍞 Углеводы", progress.Carbs, progress.CarbsLimit, progress.CarbsRemaining, progress.IsCarbsExceeded, progress.CarbsExceededBy);
    }

    private static void AppendMacroTargetLine(
        StringBuilder builder, string label, decimal consumed, decimal? limit, decimal remaining, bool isExceeded, decimal exceededBy)
    {
        if (limit is null or 0m)
        {
            builder.AppendLine($"{label}: {Num(consumed)} г");
            return;
        }

        var percent = (int)Math.Round(consumed * 100m / limit.Value, MidpointRounding.AwayFromZero);
        var status = isExceeded ? $"⚠️ перебор на {Num(exceededBy)} г" : $"осталось {Num(remaining)} г";

        builder.AppendLine($"{label}: {Num(consumed)} из {Num(limit.Value)} г ({percent}%) — {status}");
        builder.AppendLine(ProgressBar(percent));
    }

    private static void AppendFittingFavorites(StringBuilder builder, DailyProgress progress)
    {
        if (progress.FittingFavorites.Count == 0)
        {
            builder.Append(progress.TrackingMode == CalorieTrackingMode.Macros
                ? "💝 Из избранного пока ничего не вписывается в остаток по БЖУ."
                : $"💝 Из избранного в остаток <b>{progress.RemainingCalories} ккал</b> пока ничего не вписывается.");
            return;
        }

        builder.AppendLine(progress.TrackingMode == CalorieTrackingMode.Macros
            ? "💝 <b>Можно съесть из любимого</b> (укладывается в остаток по БЖУ):"
            : $"💝 <b>Можно съесть из любимого</b> (до {progress.RemainingCalories} ккал):");

        foreach (var product in progress.FittingFavorites)
        {
            builder.AppendLine($"• {FavoriteLine(product)}");
        }
    }

    /// <summary>Список того, что записано в текущем цикле, сгруппированный по типам приёмов пищи.</summary>
    private static void AppendCycleHistory(StringBuilder builder, IReadOnlyList<FoodLogEntry> entries, TimeSpan offset)
    {
        builder.AppendLine("📋 <b>История цикла</b>");
        builder.AppendLine();

        if (entries.Count == 0)
        {
            builder.Append("Пока ничего не записано.");
            return;
        }

        foreach (var group in entries.GroupBy(e => e.MealType).OrderBy(g => g.Key))
        {
            var groupCalories = group.Sum(e => e.Calories);
            builder.AppendLine($"{MealTypeName(group.Key)} — <b>{groupCalories} ккал</b>");

            foreach (var entry in group.OrderBy(e => e.LoggedAt))
            {
                var localTime = ToLocal(entry.LoggedAt, offset);
                var serving = string.IsNullOrWhiteSpace(entry.ServingSize) ? string.Empty : $", {Escape(entry.ServingSize!)}";
                var favoriteMark = entry.IsFavorite ? " ⭐" : string.Empty;

                // Цикл может растянуться дольше суток, поэтому дату в отметке времени не прячу.
                builder.AppendLine(
                    $"  • {localTime:dd.MM HH:mm} {Escape(entry.ProductName)}{serving} — {entry.Calories} ккал{favoriteMark}");
            }

            builder.AppendLine();
        }
    }

    /// <summary>Ответ после записи приёма пищи: что записал и что стало с лимитом.</summary>
    public static string MealLogged(FoodLogEntry entry, DailyProgress progress)
    {
        var serving = string.IsNullOrWhiteSpace(entry.ServingSize) ? string.Empty : $" ({Escape(entry.ServingSize!)})";
        var builder = new StringBuilder();

        builder.AppendLine($"✅ Записал: <b>{Escape(entry.ProductName)}</b>{serving}");
        builder.AppendLine($"{MealTypeName(entry.MealType)} · <b>{entry.Calories} ккал</b>");
        builder.AppendLine($"Б {Num(entry.Proteins)} · Ж {Num(entry.Fats)} · У {Num(entry.Carbs)} г");
        builder.AppendLine();

        if (progress.TrackingMode == CalorieTrackingMode.Macros)
        {
            AppendMacroBreakdown(builder, progress);
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine($"Съедено <b>{progress.ConsumedCalories}</b> из <b>{progress.CalorieLimit}</b> ккал ({progress.PercentUsed}%).");

        if (progress.IsExceeded)
        {
            builder.AppendLine($"⚠️ <b>Лимит превышен на {progress.ExceededBy} ккал!</b>");
        }
        else
        {
            builder.AppendLine($"Осталось: <b>{progress.RemainingCalories}</b> ккал");
        }

        builder.Append(ProgressBar(progress.PercentUsed));
        return builder.ToString();
    }

    /// <summary>Спрашиваю подтверждение перед закрытием текущего цикла.</summary>
    public static string AskNewDayConfirm(DailyProgress progress, TimeSpan offset)
    {
        var cycleStart = ToLocal(progress.CycleStartedAt, offset);

        var builder = new StringBuilder();
        builder.AppendLine("🆕 <b>Начать новый день?</b>");
        builder.AppendLine();
        builder.AppendLine($"Текущий цикл идёт с {cycleStart:dd.MM HH:mm} ({Duration(progress.CycleElapsed)}).");
        builder.AppendLine($"Съедено: <b>{progress.ConsumedCalories}</b> из <b>{progress.CalorieLimit}</b> ккал ({progress.PercentUsed}%).");
        builder.AppendLine();
        builder.Append("Он сохранится в «📅 История», а счётчик обнулится и пойдёт заново.");

        return builder.ToString();
    }

    /// <summary>Подтверждение того, что новый цикл начат.</summary>
    public static string NewDayStarted(CalorieCycle closedCycle) =>
        $"✅ Новый день начат — счётчик обнулён.\n\n" +
        $"Прошлый цикл сохранён в историю: <b>{closedCycle.ConsumedCalories}</b> из <b>{closedCycle.CalorieLimit}</b> ккал.";

    /// <summary>Список прошлых циклов, новые сверху.</summary>
    public static string CycleHistoryPage(IReadOnlyList<CalorieCycle> cycles, int page, int pageSize, int totalCount, TimeSpan offset)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"📅 <b>История</b> ({totalCount})");
        builder.AppendLine();

        var index = page * pageSize + 1;
        foreach (var cycle in cycles)
        {
            var start = ToLocal(cycle.StartedAt, offset);
            var end = ToLocal(cycle.EndedAt, offset);
            var percent = cycle.CalorieLimit <= 0 ? 0 : (int)Math.Round(cycle.ConsumedCalories * 100m / cycle.CalorieLimit, MidpointRounding.AwayFromZero);
            var mark = cycle.ConsumedCalories > cycle.CalorieLimit ? " ⚠️" : string.Empty;

            builder.AppendLine($"<b>{index}. {start:dd.MM HH:mm}</b> → {end:dd.MM HH:mm} ({Duration(end - start)})");
            builder.AppendLine(
                $"   🔥 {cycle.ConsumedCalories} из {cycle.CalorieLimit} ккал ({percent}%){mark} · " +
                $"Б {Num(cycle.Proteins)} · Ж {Num(cycle.Fats)} · У {Num(cycle.Carbs)} · записей: {cycle.EntriesCount}");
            index++;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Ниже этой разницы отклонение считаю шумом округления, а не поводом для рекомендации.</summary>
    private const int CalorieRecommendationThreshold = 50;

    private const decimal MacroRecommendationThresholdGrams = 5m;

    /// <summary>Отчёт за неделю/месяц — сводка и текстовая рекомендация. Лимит пользователя не меняет.</summary>
    public static string PeriodReport(PeriodReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"📈 <b>Отчёт за {report.PeriodDays} дн.</b>");
        builder.AppendLine();

        if (report.DaysWithData == 0)
        {
            builder.Append("За этот период записей не было — отчёт появится, когда наберётся история.");
            return builder.ToString();
        }

        builder.AppendLine($"Дней с записями: <b>{report.DaysWithData}</b> из {report.PeriodDays}");
        builder.AppendLine();

        if (report.TrackingMode == CalorieTrackingMode.Macros)
        {
            builder.AppendLine($"В среднем за день: Б {Num(report.AverageProteins)} · Ж {Num(report.AverageFats)} · У {Num(report.AverageCarbs)} г");
            builder.AppendLine(
                $"Ориентиры: Б {Num(report.ProteinsLimit ?? 0m)} · Ж {Num(report.FatsLimit ?? 0m)} · У {Num(report.CarbsLimit ?? 0m)} г");
        }
        else
        {
            builder.AppendLine($"Среднее потребление: <b>{report.AverageCalories}</b> из <b>{report.CalorieLimit}</b> ккал/день");
            builder.AppendLine($"Дней с перебором: {report.DaysOverCalorieLimit} · дней с недобором: {report.DaysUnderCalorieLimit}");
            builder.AppendLine($"В среднем за день: Б {Num(report.AverageProteins)} · Ж {Num(report.AverageFats)} · У {Num(report.AverageCarbs)} г");
        }

        var recommendation = BuildPeriodRecommendation(report);
        if (recommendation is not null)
        {
            builder.AppendLine();
            builder.Append(recommendation);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Рекомендация «выровнять» по последнему завершённому дню — распределяю отклонение на ближайшие
    /// 1-2 дня. Только текст: сам дневной лимит пользователя эта рекомендация не меняет.
    /// </summary>
    private static string? BuildPeriodRecommendation(PeriodReport report)
    {
        if (report.LastDayWithData is null)
        {
            return null;
        }

        if (report.TrackingMode == CalorieTrackingMode.Macros)
        {
            var lines = new List<string>();
            AppendMacroRecommendation(lines, "белка", report.LastDayProteins, report.ProteinsLimit);
            AppendMacroRecommendation(lines, "жира", report.LastDayFats, report.FatsLimit);
            AppendMacroRecommendation(lines, "углеводов", report.LastDayCarbs, report.CarbsLimit);

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        var diff = report.CalorieLimit - report.LastDayCalories;
        if (Math.Abs(diff) < CalorieRecommendationThreshold)
        {
            return null;
        }

        var perDay = diff / 2;
        var adjusted = report.CalorieLimit + perDay;
        var direction = diff > 0 ? "не добрали" : "перебрали";

        return $"Вчера {direction} по калориям на {Math.Abs(diff)} ккал → на ближайшие 1-2 дня можно " +
               $"ориентироваться на ~{adjusted} ккал вместо {report.CalorieLimit} (рекомендация, лимит не меняю).";
    }

    private static void AppendMacroRecommendation(List<string> lines, string label, decimal consumed, decimal? limit)
    {
        if (limit is null or 0m)
        {
            return;
        }

        var diff = limit.Value - consumed;
        if (Math.Abs(diff) < MacroRecommendationThresholdGrams)
        {
            return;
        }

        var perDay = Math.Round(diff / 2m, 1);
        var adjusted = Math.Round(limit.Value + perDay, 1);
        var direction = diff > 0 ? "не добрали" : "перебрали";

        lines.Add(
            $"Вчера {direction} {label} на {Num(Math.Abs(diff))} г → на ближайшие 1-2 дня ориентир " +
            $"~{Num(adjusted)} г вместо {Num(limit.Value)} (лимит не меняю).");
    }

    public const string EmptyCycleHistory =
        "📅 Прошлых циклов пока нет.\n\nОни появятся здесь после того, как вы хотя бы раз нажмёте «🆕 Новый день».";

    /// <summary>Карточка одного прошлого цикла — открывается по тапу из «Истории», записи представлены кнопками ниже.</summary>
    public static string CycleDetailsCard(CalorieCycle cycle, IReadOnlyList<FoodLogEntry> entries, TimeSpan offset)
    {
        var start = ToLocal(cycle.StartedAt, offset);
        var end = ToLocal(cycle.EndedAt, offset);
        var percent = cycle.CalorieLimit <= 0 ? 0 : (int)Math.Round(cycle.ConsumedCalories * 100m / cycle.CalorieLimit, MidpointRounding.AwayFromZero);
        var mark = cycle.ConsumedCalories > cycle.CalorieLimit ? " ⚠️" : string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine($"📅 <b>Цикл {start:dd.MM HH:mm} → {end:dd.MM HH:mm}</b> ({Duration(end - start)})");
        builder.AppendLine(
            $"🔥 {cycle.ConsumedCalories} из {cycle.CalorieLimit} ккал ({percent}%){mark} · " +
            $"Б {Num(cycle.Proteins)} · Ж {Num(cycle.Fats)} · У {Num(cycle.Carbs)} · записей: {cycle.EntriesCount}");
        builder.AppendLine();

        builder.Append(entries.Count == 0
            ? "Записей нет — можно добавить кнопкой ниже."
            : "Нажмите на запись ниже, чтобы удалить её, либо добавьте новую.");

        return builder.ToString();
    }

    /// <summary>Подпись кнопки-записи в карточке цикла — тап по ней запрашивает удаление.</summary>
    public static string EntryButtonLabel(FoodLogEntry entry, TimeSpan offset)
    {
        var localTime = ToLocal(entry.LoggedAt, offset);
        var name = entry.ProductName.Length > 20 ? entry.ProductName[..19] + "…" : entry.ProductName;
        return $"🗑 {localTime:dd.MM HH:mm} {name} — {entry.Calories} ккал";
    }

    /// <summary>Запрос подтверждения удаления записи из прошлого цикла.</summary>
    public static string ConfirmDeleteCycleEntry(FoodLogEntry entry, TimeSpan offset) =>
        $"🗑 Удалить эту запись из цикла?\n\n" +
        $"{ToLocal(entry.LoggedAt, offset):dd.MM HH:mm} — <b>{Escape(entry.ProductName)}</b>\n" +
        $"{MealTypeName(entry.MealType)} · {entry.Calories} ккал";

    /// <summary>Экран «Текущий лимит».</summary>
    public static string CurrentLimit(AppUser user, DailyProgress progress, TimeSpan offset)
    {
        var builder = new StringBuilder();
        builder.AppendLine("🎯 <b>Дневной лимит</b>");
        builder.AppendLine();

        if (user.TrackingMode == CalorieTrackingMode.Macros)
        {
            builder.AppendLine("Режим: <b>по БЖУ</b> 🥩");
            builder.AppendLine(
                $"Лимиты: Б {Num(user.DailyProteinsLimit ?? 0m)} / Ж {Num(user.DailyFatsLimit ?? 0m)} / У {Num(user.DailyCarbsLimit ?? 0m)} г");
            builder.AppendLine($"Это примерно <b>{user.DailyCalorieLimit} ккал</b> (справочно).");
        }
        else
        {
            builder.AppendLine("Режим: <b>по калориям</b> 📐");
            builder.AppendLine($"Лимит: <b>{user.DailyCalorieLimit} ккал</b>");

            if (user.DailyProteinsLimit is not null)
            {
                builder.AppendLine(
                    $"Ориентиры по БЖУ: {Num(user.DailyProteinsLimit.Value)} / {Num(user.DailyFatsLimit ?? 0m)} / {Num(user.DailyCarbsLimit ?? 0m)} г");
            }
        }

        builder.AppendLine();
        builder.AppendLine(user.GoalSetAt is null
            ? "Лимит стоит по умолчанию — вы его ещё не меняли."
            : $"Установлен: {ToLocal(user.GoalSetAt.Value, offset):dd.MM.yyyy HH:mm} (UTC+3)");

        builder.AppendLine("<i>Лимит действует постоянно, пока вы сами его не замените.</i>");
        builder.AppendLine();

        builder.Append(user.TrackingMode == CalorieTrackingMode.Macros
            ? $"В этом цикле осталось: Б {Num(progress.ProteinsRemaining)} · Ж {Num(progress.FatsRemaining)} · У {Num(progress.CarbsRemaining)} г."
            : $"В этом цикле съедено: <b>{progress.ConsumedCalories}</b> ккал, осталось <b>{progress.RemainingCalories}</b> ккал.");

        return builder.ToString();
    }

    /// <summary>Спрашиваю, что именно пользователь хочет отслеживать.</summary>
    public const string AskLimitMode =
        "Как вам удобнее вести учёт?\n\n" +
        "📐 <b>По калориям</b> — задаёте дневной максимум ккал, БЖУ считаются справочно (30/30/40 %).\n" +
        "🥩 <b>По БЖУ</b> — задаёте лимиты белков/жиров/углеводов напрямую, калории — расчётная величина.";

    /// <summary>Запрос нового лимита калорий.</summary>
    public static string AskCalorieLimit(int currentLimit) =>
        $"Текущий дневной максимум: <b>{currentLimit} ккал</b>.\n\n" +
        $"Отправьте новое значение числом — от {InputParser.MinCalorieLimit} до {InputParser.MaxCalorieLimit} ккал.\n" +
        "Например: <code>1800</code>";

    /// <summary>Запрос новых дневных лимитов БЖУ.</summary>
    public const string AskMacroLimits =
        "Пришлите дневные лимиты БЖУ в граммах — три числа в порядке <b>белки жиры углеводы</b>, " +
        MacrosFormatHint + "\n\n" +
        "Например: <code>150 60 250</code>";

    /// <summary>Подтверждение смены лимита калорий.</summary>
    public static string LimitUpdated(AppUser user, DailyProgress progress)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"✅ Новый дневной максимум: <b>{user.DailyCalorieLimit} ккал</b>");

        if (user.DailyProteinsLimit is not null)
        {
            builder.AppendLine(
                $"Ориентиры по БЖУ: {Num(user.DailyProteinsLimit.Value)} / {Num(user.DailyFatsLimit ?? 0m)} / {Num(user.DailyCarbsLimit ?? 0m)} г");
        }

        builder.AppendLine();
        builder.AppendLine($"В этом цикле уже съедено <b>{progress.ConsumedCalories}</b> ккал.");
        builder.Append(progress.IsExceeded
            ? $"⚠️ <b>С новым лимитом это перебор на {progress.ExceededBy} ккал.</b>"
            : $"Осталось: <b>{progress.RemainingCalories}</b> ккал");

        return builder.ToString();
    }

    /// <summary>Подтверждение смены лимитов БЖУ.</summary>
    public static string MacroLimitsUpdated(AppUser user, DailyProgress progress)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"✅ Новые дневные лимиты: Б {Num(user.DailyProteinsLimit ?? 0m)} / Ж {Num(user.DailyFatsLimit ?? 0m)} / У {Num(user.DailyCarbsLimit ?? 0m)} г");
        builder.AppendLine($"Это примерно <b>{user.DailyCalorieLimit} ккал</b> (справочно).");
        builder.AppendLine();
        builder.AppendLine(
            $"В этом цикле уже съедено: Б {Num(progress.Proteins)} · Ж {Num(progress.Fats)} · У {Num(progress.Carbs)} г.");

        if (progress.IsProteinsExceeded || progress.IsFatsExceeded || progress.IsCarbsExceeded)
        {
            builder.Append("⚠️ С новыми лимитами по некоторым нутриентам уже перебор.");
        }
        else
        {
            builder.Append(
                $"Осталось: Б {Num(progress.ProteinsRemaining)} · Ж {Num(progress.FatsRemaining)} · У {Num(progress.CarbsRemaining)} г.");
        }

        return builder.ToString();
    }

    /// <summary>Заголовок списка избранного при выборе продукта для дневника.</summary>
    public static string PickFavoriteHeader(DailyProgress progress, int totalFavorites)
    {
        var builder = new StringBuilder();
        builder.AppendLine("💝 <b>Выберите продукт</b>");
        builder.AppendLine();
        builder.AppendLine($"В избранном: {totalFavorites}");

        if (progress.TrackingMode == CalorieTrackingMode.Macros)
        {
            builder.Append(
                $"Осталось: Б {Num(progress.ProteinsRemaining)} · Ж {Num(progress.FatsRemaining)} · У {Num(progress.CarbsRemaining)} г. " +
                "Продукты, которые не вписываются, помечены знаком ⚠️ (для порций с плавающим весом — не помечаются).");
            return builder.ToString();
        }

        builder.Append(progress.IsExceeded
            ? $"⚠️ Лимит уже превышен на {progress.ExceededBy} ккал."
            : $"Осталось до лимита: <b>{progress.RemainingCalories}</b> ккал. Продукты, которые не вписываются, помечены знаком ⚠️.");

        return builder.ToString();
    }

    /// <summary>Карточка продукта плюс вопрос о типе приёма пищи — последний шаг записи в дневник.</summary>
    public static string ChooseMealType(ProductDraft draft) =>
        $"{ProductCard(draft)}\n\n🍽 <b>Когда вы это съели?</b>";

    /// <summary>Предупреждение: продукт с таким названием уже записан в этом цикле.</summary>
    public static string AskDuplicateMealConfirm(FoodLogEntry existing, ProductDraft draft) =>
        $"⚠️ Вы уже записывали <b>{Escape(existing.ProductName)}</b> в этом цикле " +
        $"({MealTypeName(existing.MealType)}, {existing.Calories} ккал).\n\n" +
        $"{ProductCard(draft)}\n\n" +
        "Заменить ту запись новой или добавить как отдельный приём пищи?";

    /// <summary>Карточка продукта плюс предложение сохранить его в избранное.</summary>
    public static string OfferToSaveFavorite(ProductDraft draft) =>
        $"{ProductCard(draft)}\n\n⭐ Сохранить продукт в избранное, чтобы добавлять его в один тап?";

    /// <summary>Итог сохранения продукта в избранное.</summary>
    public static string FavoriteSaved(FavoriteProduct product, bool created)
    {
        var header = created ? "✅ Добавил в избранное:" : "♻️ Продукт уже был в избранном — обновил его КБЖУ:";
        return $"{header}\n\n{ProductCard(ProductDraft.FromFavorite(product))}";
    }

    /// <summary>Заголовок списка удаления.</summary>
    public static string DeleteListHeader(int totalFavorites) =>
        $"🗑 <b>Удаление из избранного</b>\n\nВыберите продукт, который нужно убрать. В избранном: {totalFavorites}.\n" +
        "<i>Записи в истории питания при этом сохранятся.</i>";

    /// <summary>Запрос подтверждения удаления.</summary>
    public static string ConfirmDelete(FavoriteProduct product) =>
        $"🗑 Удалить из избранного?\n\n{ProductCard(ProductDraft.FromFavorite(product))}";

    /// <summary>Итог удаления.</summary>
    public static string FavoriteDeleted(string name) =>
        $"🗑 Удалил <b>{Escape(name)}</b> из избранного.";

    /// <summary>Сообщение об ошибке ввода — добавляю к тексту валидатора подсказку, что делать.</summary>
    public static string ValidationError(string error) => $"⚠️ {error}\n\nПопробуйте ещё раз.";

    /// <summary>Заголовок списка «Мои продукты» — сам список кликабельный (карточка на тап).</summary>
    public static string MyProductsHeader(int totalFavorites) =>
        $"📝 <b>Мои продукты</b> ({totalFavorites})\n\nВыберите продукт, чтобы посмотреть или изменить его.";

    /// <summary>БЖУ элемента «Воды» — всегда на 1 литр, отдельная подсказка вместо общей «на 100 г / на порцию».</summary>
    public const string AskMacrosPerLiter =
        "Пришлите БЖУ <b>на 1 литр</b> — три числа в порядке <b>белки жиры углеводы</b>, " +
        MacrosFormatHint + "\n\nДля обычной воды подойдёт <code>0 0 0</code>… впрочем, значения БЖУ должны быть больше нуля — " +
        "если жидкость калорийная (морс, сок), укажите её реальное БЖУ. Например: <code>0.1 0 5</code>";

    /// <summary>Заголовок списка «Вода».</summary>
    public static string WaterListHeader(int totalItems) =>
        $"💧 <b>Вода</b> ({totalItems})\n\nВыберите жидкость, чтобы посмотреть или изменить её, либо добавьте новую.";

    /// <summary>Спрашиваю название новой жидкости для «Воды».</summary>
    public const string AskWaterName =
        "Как называется жидкость? Например «Вода» или «Морс».\n\nОтправьте название сообщением.";

    /// <summary>Заголовок списка «Готовые блюда».</summary>
    public static string DishListHeader(int totalDishes) =>
        $"🍲 <b>Готовые блюда</b> ({totalDishes})\n\nВыберите блюдо, чтобы посмотреть состав, либо создайте новое.";

    /// <summary>Спрашиваю название нового блюда.</summary>
    public const string AskDishName =
        "Как называется блюдо? Например «Овсянка с бананом».\n\nОтправьте название сообщением.";

    /// <summary>Карточка блюда: КБЖУ автосуммой по ингредиентам, список ингредиентов — кнопками ниже.</summary>
    public static string DishCard(FavoriteProduct dish)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"🍲 <b>{Escape(dish.Name)}</b>");
        builder.AppendLine();
        builder.AppendLine($"🔥 {dish.Calories} ккал · Б {Num(dish.Proteins)} · Ж {Num(dish.Fats)} · У {Num(dish.Carbs)} г");
        builder.Append("<i>КБЖУ считаются автосуммой ингредиентов — правьте состав, а не эти числа напрямую.</i>");

        return builder.ToString();
    }

    /// <summary>Спрашиваю название ингредиента, добавляемого в блюдо.</summary>
    public const string AskIngredientName =
        "Название ингредиента? Отправьте сообщением.";

    /// <summary>Подпись кнопки ингредиента в карточке блюда — тап по ней запрашивает удаление.</summary>
    public static string IngredientButtonLabel(DishIngredient ingredient)
    {
        var name = ingredient.Name.Length > 24 ? ingredient.Name[..23] + "…" : ingredient.Name;
        return $"🗑 {name} — {ingredient.Calories} ккал";
    }

    /// <summary>Запрос подтверждения удаления ингредиента.</summary>
    public static string ConfirmDeleteIngredient(DishIngredient ingredient) =>
        $"🗑 Убрать ингредиент из блюда?\n\n<b>{Escape(ingredient.Name)}</b> — {ingredient.Calories} ккал " +
        $"(Б {Num(ingredient.Proteins)} · Ж {Num(ingredient.Fats)} · У {Num(ingredient.Carbs)} г)";

    /// <summary>Заголовок экрана подкатегорий «Продуктов».</summary>
    public static string ProductCategoriesHeader(bool manageMode) => manageMode
        ? "✏️ <b>Управление подкатегориями</b>\n\nВыберите подкатегорию, чтобы переименовать или удалить."
        : "🥘 <b>Продукты</b>\n\nВыберите подкатегорию.";

    /// <summary>Заголовок списка продуктов внутри одной подкатегории.</summary>
    public static string ProductsInCategoryHeader(string categoryName, int totalItems) =>
        $"🥘 <b>{Escape(categoryName)}</b> ({totalItems})\n\nВыберите продукт, чтобы посмотреть или изменить его.";

    /// <summary>Спрашиваю название новой подкатегории.</summary>
    public const string AskNewCategoryName =
        "Название новой подкатегории? Отправьте сообщением.";

    /// <summary>Спрашиваю новое название подкатегории при переименовании.</summary>
    public const string AskCategoryRename =
        "Новое название подкатегории? Отправьте сообщением.";

    /// <summary>Итог создания подкатегории.</summary>
    public static string CategoryCreated(string name) =>
        $"✅ Добавил подкатегорию «<b>{Escape(name)}</b>».";

    /// <summary>Итог переименования подкатегории.</summary>
    public static string CategoryRenamed(string name) =>
        $"✅ Подкатегория теперь называется «<b>{Escape(name)}</b>».";

    /// <summary>Карточка подкатегории в режиме управления.</summary>
    public static string CategoryManageCard(ProductCategory category) =>
        $"<b>{Escape(category.Name)}</b>{(category.IsBuiltIn ? " <i>(базовая)</i>" : string.Empty)}";

    /// <summary>Запрос подтверждения удаления подкатегории.</summary>
    public static string ConfirmDeleteCategory(ProductCategory category) =>
        $"🗑 Удалить подкатегорию «<b>{Escape(category.Name)}</b>»?\n\n" +
        "Продукты внутри неё не удалятся — просто окажутся «без подкатегории».";

    /// <summary>Итог удаления подкатегории.</summary>
    public static string CategoryDeleted(string name) =>
        $"🗑 Удалил подкатегорию «<b>{Escape(name)}</b>».";

    /// <summary>Спрашиваю, в какую подкатегорию перенести продукт.</summary>
    public static string AskMoveCategory(FavoriteProduct product) =>
        $"📂 В какую подкатегорию перенести «<b>{Escape(product.Name)}</b>»?";

    /// <summary>Итог переноса продукта в другую подкатегорию.</summary>
    public static string FavoriteMoved(FavoriteProduct product, string? newCategoryName) =>
        $"📂 «<b>{Escape(product.Name)}</b>» теперь в " +
        (newCategoryName is null ? "«📦 Без категории»." : $"«<b>{Escape(newCategoryName)}</b>».");

    /// <summary>Карточка продукта с деталями и типом порции — экран редактирования.</summary>
    public static string FavoriteDetailsCard(FavoriteProduct product)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<b>{Escape(product.Name)}</b>");
        builder.AppendLine();

        if (product.IsFixedServing)
        {
            var serving = string.IsNullOrWhiteSpace(product.ServingSize) ? string.Empty : $" ({Escape(product.ServingSize!)})";
            builder.AppendLine($"🍽 Фиксированная порция{serving}");
            builder.AppendLine($"🔥 {product.Calories} ккал · Б {Num(product.Proteins)} · Ж {Num(product.Fats)} · У {Num(product.Carbs)} г");
        }
        else if (product.CategoryKind == FavoriteCategoryKind.Water)
        {
            builder.AppendLine("📏 Порция на 1 литр — объём спрашивается при добавлении в дневник");
            builder.AppendLine($"🔥 {product.Calories} ккал · Б {Num(product.Proteins)} · Ж {Num(product.Fats)} · У {Num(product.Carbs)} г — <i>на 1 л</i>");
        }
        else
        {
            builder.AppendLine("📏 Порция на 100 г — вес спрашивается при добавлении в дневник");
            builder.AppendLine($"🔥 {product.Calories} ккал · Б {Num(product.Proteins)} · Ж {Num(product.Fats)} · У {Num(product.Carbs)} г — <i>на 100 г</i>");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Одна строка списка избранного.</summary>
    public static string FavoriteLine(FavoriteProduct product)
    {
        var serving = string.IsNullOrWhiteSpace(product.ServingSize) ? string.Empty : $" ({Escape(product.ServingSize!)})";
        return $"{Escape(product.Name)}{serving} — <b>{product.Calories}</b> ккал";
    }

    /// <summary>Подпись кнопки продукта. Слишком длинные названия обрезаю, иначе кнопка расползается.</summary>
    public static string ProductButtonLabel(FavoriteProduct product, bool fitsIntoLimit)
    {
        var name = product.Name.Length > 28 ? product.Name[..27] + "…" : product.Name;
        var mark = fitsIntoLimit ? string.Empty : "⚠️ ";
        return $"{mark}{name} — {product.Calories} ккал";
    }

    /// <summary>Строка по одному макронутриенту: съедено и ориентир, если он посчитан.</summary>
    private static string MacroLine(string label, decimal consumed, decimal? target) =>
        target is null or 0m
            ? $"{label}: {Num(consumed)} г"
            : $"{label}: {Num(consumed)} / {Num(target.Value)} г";

    public static string MealTypeName(MealType mealType) => mealType switch
    {
        MealType.Breakfast => "🍳 Завтрак",
        MealType.Lunch => "🍲 Обед",
        MealType.Dinner => "🍝 Ужин",
        MealType.Snack => "🍎 Перекус",
        _ => "🍽 Приём пищи"
    };

    /// <summary>Экранирую пользовательский текст для HTML-разметки Telegram.</summary>
    public static string Escape(string value) => WebUtility.HtmlEncode(value);

    /// <summary>Числа показываю без лишних нулей: 12, 12.5, 0.75.</summary>
    public static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Полоска прогресса из десяти делений. Перебор подсвечиваю отдельным символом.</summary>
    public static string ProgressBar(int percent)
    {
        const int cells = 10;
        var filled = Math.Clamp((int)Math.Round(percent / 100.0 * cells), 0, cells);
        var bar = new string('▰', filled) + new string('▱', cells - filled);
        return percent > 100 ? $"{bar} {percent}% ⚠️" : $"{bar} {percent}%";
    }

    /// <summary>Перевожу время из UTC в локальное для показа пользователю.</summary>
    private static DateTimeOffset ToLocal(DateTime utc, TimeSpan offset) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(offset);

    /// <summary>Человеческая длительность: «5 ч 20 мин».</summary>
    public static string Duration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "меньше минуты";
        }

        var hours = (int)value.TotalHours;
        var minutes = value.Minutes;

        return hours > 0 ? $"{hours} ч {minutes} мин" : $"{minutes} мин";
    }
}

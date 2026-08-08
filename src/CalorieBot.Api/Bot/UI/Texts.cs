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
    /// <summary>Названия месяцев держу своим списком, чтобы не зависеть от наличия ICU в контейнере.</summary>
    private static readonly string[] MonthsGenitive =
    {
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    };

    public const string OnlyButtons =
        "Я понимаю только кнопки 🙂\nВыберите действие в меню ниже.";

    public const string StaleDialog =
        "Этот диалог уже неактуален — начните заново из меню.";

    public const string SomethingWentWrong =
        "😔 Что-то пошло не так. Я уже записал ошибку в лог — попробуйте ещё раз через меню.";

    public const string ChooseMealSource =
        "Что добавляем?\n\n💝 <b>Из любимых</b> — выбрать из сохранённых продуктов\n🔍 <b>Новый продукт</b> — ввести название и БЖУ";

    public const string FavoritesMenuText =
        "⭐ <b>Любимые продукты</b>\n\nЗдесь я храню продукты, которые вы едите чаще всего — их можно добавлять в дневник одним нажатием.";

    public static readonly string AskProductName =
        $"Как называется продукт?\n\nОтправьте название сообщением (от {InputParser.MinNameLength} до {InputParser.MaxNameLength} символов).";

    public const string AskMacros =
        "Теперь пришлите БЖУ <b>на порцию</b> в граммах — три числа в порядке <b>белки жиры углеводы</b>.\n\n" +
        "Например: <code>12 5 30</code>\n\nКалории я посчитаю сам по формуле Б×4 + Ж×9 + У×4.";

    public const string AskServingSize =
        "Опишите порцию — например <code>200 г</code> или <code>1 стакан</code>.\n\n" +
        "Если не нужно, нажмите «⏭ Пропустить».";

    public const string EmptyFavorites =
        "⭐ Избранное пока пусто.\n\nДобавьте продукты через <b>⭐ Любимые продукты → ➕ Добавить в избранное</b> — потом их можно будет записывать в один тап.";

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
        builder.AppendLine("📊 посмотреть, сколько ещё можно");
        builder.AppendLine("⭐ хранить любимые продукты");
        builder.AppendLine("🎯 менять дневной максимум");
        builder.AppendLine("📋 смотреть, что съедено сегодня");
        builder.AppendLine();
        builder.Append("<i>Счётчик обнуляется в полночь по московскому времени (UTC+3).</i>");

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

    /// <summary>Главный экран прогресса.</summary>
    public static string Progress(DailyProgress progress)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"📊 <b>Прогресс за {LocalDate(progress.LocalDate)}</b>");
        builder.AppendLine();
        builder.AppendLine($"Съедено <b>{progress.ConsumedCalories}</b> из <b>{progress.CalorieLimit}</b> ккал ({progress.PercentUsed}% от лимита).");

        if (progress.IsExceeded)
        {
            builder.AppendLine("Осталось: <b>0</b> ккал");
            builder.AppendLine(ProgressBar(progress.PercentUsed));
            builder.AppendLine();
            builder.AppendLine($"⚠️ <b>Лимит превышен на {progress.ExceededBy} ккал!</b>");
            builder.AppendLine("Сегодня лучше остановиться — а завтра счётчик начнётся с нуля.");
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
        builder.AppendLine();
        builder.AppendLine($"Приёмов пищи сегодня: <b>{progress.EntriesCount}</b>");
        builder.AppendLine($"До сброса счётчика: {Duration(progress.TimeUntilReset)}");
        builder.AppendLine();

        if (progress.IsExceeded || progress.RemainingCalories == 0)
        {
            builder.Append("💝 Лимит на сегодня исчерпан, подходящих продуктов не показываю.");
        }
        else if (progress.FittingFavorites.Count == 0)
        {
            builder.Append($"💝 Из избранного в остаток <b>{progress.RemainingCalories} ккал</b> пока ничего не вписывается.");
        }
        else
        {
            builder.AppendLine($"💝 <b>Можно съесть из любимого</b> (до {progress.RemainingCalories} ккал):");
            foreach (var product in progress.FittingFavorites)
            {
                builder.AppendLine($"• {FavoriteLine(product)}");
            }
        }

        return builder.ToString().TrimEnd();
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

    /// <summary>История за сегодня, сгруппированная по типам приёмов пищи.</summary>
    public static string History(IReadOnlyList<FoodLogEntry> entries, DailyProgress progress, TimeSpan offset)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"📋 <b>История за {LocalDate(progress.LocalDate)}</b>");
        builder.AppendLine();

        if (entries.Count == 0)
        {
            builder.AppendLine("Сегодня вы ещё ничего не записали.");
            builder.AppendLine();
            builder.Append($"Дневной лимит: <b>{progress.CalorieLimit} ккал</b> — весь ваш.");
            return builder.ToString();
        }

        foreach (var group in entries.GroupBy(e => e.MealType).OrderBy(g => g.Key))
        {
            var groupCalories = group.Sum(e => e.Calories);
            builder.AppendLine($"{MealTypeName(group.Key)} — <b>{groupCalories} ккал</b>");

            foreach (var entry in group.OrderBy(e => e.LoggedAt))
            {
                var localTime = new DateTimeOffset(DateTime.SpecifyKind(entry.LoggedAt, DateTimeKind.Utc)).ToOffset(offset);
                var serving = string.IsNullOrWhiteSpace(entry.ServingSize) ? string.Empty : $", {Escape(entry.ServingSize!)}";
                var favoriteMark = entry.IsFavorite ? " ⭐" : string.Empty;

                builder.AppendLine(
                    $"  • {localTime:HH:mm} {Escape(entry.ProductName)}{serving} — {entry.Calories} ккал{favoriteMark}");
            }

            builder.AppendLine();
        }

        builder.AppendLine($"Итого: <b>{progress.ConsumedCalories}</b> из <b>{progress.CalorieLimit}</b> ккал ({progress.PercentUsed}%).");
        builder.Append(progress.IsExceeded
            ? $"⚠️ <b>Лимит превышен на {progress.ExceededBy} ккал!</b>"
            : $"Осталось: <b>{progress.RemainingCalories}</b> ккал");

        return builder.ToString();
    }

    /// <summary>Экран «Текущий лимит».</summary>
    public static string CurrentLimit(AppUser user, DailyProgress progress, TimeSpan offset)
    {
        var builder = new StringBuilder();
        builder.AppendLine("🎯 <b>Дневной максимум</b>");
        builder.AppendLine();
        builder.AppendLine($"Лимит калорий: <b>{user.DailyCalorieLimit} ккал</b>");

        if (user.DailyProteinsLimit is not null)
        {
            builder.AppendLine(
                $"Ориентиры по БЖУ: {Num(user.DailyProteinsLimit.Value)} / {Num(user.DailyFatsLimit ?? 0m)} / {Num(user.DailyCarbsLimit ?? 0m)} г");
        }

        builder.AppendLine();
        builder.AppendLine(user.GoalSetAt is null
            ? "Лимит стоит по умолчанию — вы его ещё не меняли."
            : $"Установлен: {new DateTimeOffset(DateTime.SpecifyKind(user.GoalSetAt.Value, DateTimeKind.Utc)).ToOffset(offset):dd.MM.yyyy HH:mm} (UTC+3)");

        builder.AppendLine("<i>Лимит действует постоянно, пока вы сами его не замените.</i>");
        builder.AppendLine();
        builder.Append($"Сегодня съедено: <b>{progress.ConsumedCalories}</b> ккал, осталось <b>{progress.RemainingCalories}</b> ккал.");

        return builder.ToString();
    }

    /// <summary>Запрос нового лимита.</summary>
    public static string AskCalorieLimit(int currentLimit) =>
        $"Текущий дневной максимум: <b>{currentLimit} ккал</b>.\n\n" +
        $"Отправьте новое значение числом — от {InputParser.MinCalorieLimit} до {InputParser.MaxCalorieLimit} ккал.\n" +
        "Например: <code>1800</code>";

    /// <summary>Подтверждение смены лимита.</summary>
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
        builder.AppendLine($"Сегодня уже съедено <b>{progress.ConsumedCalories}</b> ккал.");
        builder.Append(progress.IsExceeded
            ? $"⚠️ <b>С новым лимитом это перебор на {progress.ExceededBy} ккал.</b>"
            : $"Осталось: <b>{progress.RemainingCalories}</b> ккал");

        return builder.ToString();
    }

    /// <summary>Заголовок списка избранного при выборе продукта для дневника.</summary>
    public static string PickFavoriteHeader(DailyProgress progress, int totalFavorites)
    {
        var builder = new StringBuilder();
        builder.AppendLine("💝 <b>Выберите продукт</b>");
        builder.AppendLine();
        builder.AppendLine($"В избранном: {totalFavorites}");

        builder.Append(progress.IsExceeded
            ? $"⚠️ Лимит уже превышен на {progress.ExceededBy} ккал."
            : $"Осталось до лимита: <b>{progress.RemainingCalories}</b> ккал. Продукты, которые не вписываются, помечены знаком ⚠️.");

        return builder.ToString();
    }

    /// <summary>Карточка продукта плюс вопрос о типе приёма пищи — последний шаг записи в дневник.</summary>
    public static string ChooseMealType(ProductDraft draft) =>
        $"{ProductCard(draft)}\n\n🍽 <b>Когда вы это съели?</b>";

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

    /// <summary>Страница списка «Мои продукты».</summary>
    public static string FavoritesPage(IReadOnlyList<FavoriteProduct> products, int page, int pageSize)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"📝 <b>Мои продукты</b> ({products.Count})");
        builder.AppendLine();

        var index = page * pageSize + 1;
        foreach (var product in products.Skip(page * pageSize).Take(pageSize))
        {
            builder.AppendLine($"<b>{index}. {Escape(product.Name)}</b>");
            var serving = string.IsNullOrWhiteSpace(product.ServingSize) ? string.Empty : $" · порция: {Escape(product.ServingSize!)}";
            builder.AppendLine($"   🔥 {product.Calories} ккал · Б {Num(product.Proteins)} · Ж {Num(product.Fats)} · У {Num(product.Carbs)}{serving}");
            index++;
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

    /// <summary>Дата в родительном падеже: «8 августа».</summary>
    public static string LocalDate(DateOnly date) => $"{date.Day} {MonthsGenitive[date.Month - 1]}";

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

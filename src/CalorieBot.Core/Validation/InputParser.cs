using System.Globalization;
using System.Text.RegularExpressions;

namespace CalorieBot.Core.Validation;

/// <summary>
/// Разбор и валидация всего, что пользователь вводит текстом.
/// Держу это в одном месте: бот кнопочный, свободного ввода мало, и он весь должен быть проверен.
/// </summary>
public static partial class InputParser
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 64;
    public const int MaxServingSizeLength = 32;

    /// <summary>Разумные границы лимита: ниже 500 ккал это уже не про питание, выше 10 000 — опечатка.</summary>
    public const int MinCalorieLimit = 500;
    public const int MaxCalorieLimit = 10000;

    /// <summary>Больше килограмма одного макронутриента в порции быть не может — считаю это ошибкой ввода.</summary>
    public const decimal MaxMacroGrams = 1000m;

    /// <summary>Вытаскиваю из строки все числа, разделитель дробной части допускаю любой.</summary>
    [GeneratedRegex(@"\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    /// <summary>Проверяю название продукта: длина, отсутствие переводов строк и служебных символов.</summary>
    public static bool TryParseProductName(string? input, out string name, out string error)
    {
        name = string.Empty;
        error = string.Empty;

        var trimmed = (input ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        trimmed = WhitespaceRegex().Replace(trimmed, " ");

        if (trimmed.Length < MinNameLength)
        {
            error = $"Название слишком короткое — нужно минимум {MinNameLength} символа.";
            return false;
        }

        if (trimmed.Length > MaxNameLength)
        {
            error = $"Название слишком длинное — максимум {MaxNameLength} символов.";
            return false;
        }

        // Команды в качестве названия продукта — почти наверняка недоразумение.
        if (trimmed.StartsWith('/'))
        {
            error = "Название не может начинаться с «/».";
            return false;
        }

        name = trimmed;
        return true;
    }

    /// <summary>
    /// Разбираю строку с БЖУ. Жду ровно три числа в порядке «белки жиры углеводы»,
    /// разделитель любой: пробел, запятая, точка с запятой, перевод строки.
    /// </summary>
    public static bool TryParseMacros(
        string? input,
        out decimal proteins,
        out decimal fats,
        out decimal carbs,
        out string error)
    {
        proteins = fats = carbs = 0m;
        error = string.Empty;

        var matches = NumberRegex().Matches(input ?? string.Empty);
        if (matches.Count != 3)
        {
            error = "Нужно ровно три числа: белки, жиры, углеводы. Например: <code>12 5 30</code>";
            return false;
        }

        var values = new decimal[3];
        for (var i = 0; i < 3; i++)
        {
            if (!TryParseDecimal(matches[i].Value, out values[i]))
            {
                error = "Не понял число. Используйте формат вида <code>12.5</code> или <code>12,5</code>.";
                return false;
            }

            if (values[i] > MaxMacroGrams)
            {
                error = $"Слишком большое значение: максимум {MaxMacroGrams:0} г на порцию.";
                return false;
            }
        }

        if (values[0] == 0 && values[1] == 0 && values[2] == 0)
        {
            error = "Хотя бы одно значение должно быть больше нуля — иначе калорийность будет нулевой.";
            return false;
        }

        proteins = values[0];
        fats = values[1];
        carbs = values[2];
        return true;
    }

    /// <summary>Разбираю новый дневной лимит калорий.</summary>
    public static bool TryParseCalorieLimit(string? input, out int limit, out string error)
    {
        limit = 0;
        error = string.Empty;

        // Убираю пробелы внутри числа: «2 000» и «2000 ккал» должны читаться одинаково.
        var normalized = AnyWhitespaceRegex().Replace(input ?? string.Empty, string.Empty);

        var matches = NumberRegex().Matches(normalized);
        if (matches.Count != 1)
        {
            error = "Отправьте одно число — новый дневной максимум калорий. Например: <code>2000</code>";
            return false;
        }

        if (!TryParseDecimal(matches[0].Value, out var value))
        {
            error = "Не понял число. Отправьте целое количество килокалорий, например <code>2000</code>.";
            return false;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded < MinCalorieLimit || rounded > MaxCalorieLimit)
        {
            error = $"Лимит должен быть от {MinCalorieLimit} до {MaxCalorieLimit} ккал.";
            return false;
        }

        limit = rounded;
        return true;
    }

    /// <summary>Проверяю описание порции — поле необязательное, поэтому ограничиваю только длину.</summary>
    public static bool TryParseServingSize(string? input, out string? servingSize, out string error)
    {
        servingSize = null;
        error = string.Empty;

        var trimmed = (input ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        trimmed = WhitespaceRegex().Replace(trimmed, " ");

        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed.Length > MaxServingSizeLength)
        {
            error = $"Слишком длинное описание порции — максимум {MaxServingSizeLength} символов.";
            return false;
        }

        servingSize = trimmed;
        return true;
    }

    private static bool TryParseDecimal(string raw, out decimal value) =>
        decimal.TryParse(
            raw.Replace(',', '.'),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex AnyWhitespaceRegex();
}

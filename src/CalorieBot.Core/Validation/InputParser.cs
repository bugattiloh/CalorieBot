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

    /// <summary>Разумные границы веса порции при пересчёте БЖУ со 100 г.</summary>
    public const int MinServingGrams = 1;
    public const int MaxServingGrams = 5000;

    /// <summary>Разумные границы объёма для «Воды», литры.</summary>
    public const decimal MinLiters = 0.05m;
    public const decimal MaxLiters = 10m;

    /// <summary>Разумные границы возраста для расчёта нормы КБЖУ.</summary>
    public const int MinAge = 10;
    public const int MaxAge = 100;

    /// <summary>Разумные границы роста в см для расчёта нормы КБЖУ.</summary>
    public const int MinHeightCm = 100;
    public const int MaxHeightCm = 250;

    /// <summary>Разумные границы веса в кг для расчёта нормы КБЖУ.</summary>
    public const decimal MinWeightKg = 20m;
    public const decimal MaxWeightKg = 300m;

    /// <summary>Вытаскиваю из строки все числа, разделитель дробной части допускаю любой.</summary>
    [GeneratedRegex(@"\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    /// <summary>
    /// Одно число БЖУ целиком: только цифры, без знака (отрицательные запрещаю явно — знак «-»
    /// в этот класс символов не входит), не больше одного знака после точки или запятой.
    /// </summary>
    [GeneratedRegex(@"^\d+([.,]\d)?$", RegexOptions.CultureInvariant)]
    private static partial Regex StrictMacroValueRegex();

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
    /// Разбираю строку с БЖУ. Жду строго три числа через пробел в порядке «белки жиры углеводы» —
    /// без отрицательных значений и не больше одного знака после запятой/точки в каждом.
    /// Формат жёсткий специально: значения БЖУ участвуют в расчёте калорий и лимитов,
    /// а «12,5 5.0;30» и подобные вольности с разделителями легко приводят к неверному вводу.
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

        var tokens = (input ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 3)
        {
            error = "Нужно ровно три числа через пробел: белки жиры углеводы. Например: <code>12 5 1</code>";
            return false;
        }

        var values = new decimal[3];
        for (var i = 0; i < 3; i++)
        {
            if (!StrictMacroValueRegex().IsMatch(tokens[i]) || !TryParseDecimal(tokens[i], out values[i]))
            {
                error = "Каждое число — без знака «минус» и не больше одного знака после запятой. Например: <code>12 5 1</code>";
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

    /// <summary>Разбираю вес порции в граммах — нужен, чтобы пересчитать БЖУ со 100 г на реальную порцию.</summary>
    public static bool TryParseServingGrams(string? input, out int grams, out string error)
    {
        grams = 0;
        error = string.Empty;

        var matches = NumberRegex().Matches(input ?? string.Empty);
        if (matches.Count != 1)
        {
            error = "Отправьте одно число — вес порции в граммах. Например: <code>150</code>";
            return false;
        }

        if (!TryParseDecimal(matches[0].Value, out var value))
        {
            error = "Не понял число. Отправьте вес порции в граммах, например <code>150</code>.";
            return false;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded < MinServingGrams || rounded > MaxServingGrams)
        {
            error = $"Вес порции должен быть от {MinServingGrams} до {MaxServingGrams} г.";
            return false;
        }

        grams = rounded;
        return true;
    }

    /// <summary>Разбираю выпитый объём в литрах — нужен, чтобы пересчитать БЖУ с 1 л на реальную порцию.</summary>
    public static bool TryParseLiters(string? input, out decimal liters, out string error)
    {
        liters = 0m;
        error = string.Empty;

        var matches = NumberRegex().Matches(input ?? string.Empty);
        if (matches.Count != 1)
        {
            error = "Отправьте одно число — сколько литров. Например: <code>0.5</code>";
            return false;
        }

        if (!TryParseDecimal(matches[0].Value, out var value))
        {
            error = "Не понял число. Отправьте объём в литрах, например <code>0.5</code>.";
            return false;
        }

        var rounded = Math.Round(value, 2);
        if (rounded < MinLiters || rounded > MaxLiters)
        {
            error = $"Объём должен быть от {MinLiters:0.##} до {MaxLiters:0.#} л.";
            return false;
        }

        liters = rounded;
        return true;
    }

    /// <summary>Разбираю возраст для расчёта нормы КБЖУ.</summary>
    public static bool TryParseAge(string? input, out int age, out string error)
    {
        age = 0;
        error = string.Empty;

        var matches = NumberRegex().Matches(input ?? string.Empty);
        if (matches.Count != 1)
        {
            error = "Отправьте одно число — возраст полных лет. Например: <code>30</code>";
            return false;
        }

        if (!TryParseDecimal(matches[0].Value, out var value))
        {
            error = "Не понял число. Отправьте возраст полных лет, например <code>30</code>.";
            return false;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded < MinAge || rounded > MaxAge)
        {
            error = $"Возраст должен быть от {MinAge} до {MaxAge} лет.";
            return false;
        }

        age = rounded;
        return true;
    }

    /// <summary>Разбираю рост в сантиметрах для расчёта нормы КБЖУ.</summary>
    public static bool TryParseHeightCm(string? input, out int heightCm, out string error)
    {
        heightCm = 0;
        error = string.Empty;

        var matches = NumberRegex().Matches(input ?? string.Empty);
        if (matches.Count != 1)
        {
            error = "Отправьте одно число — рост в сантиметрах. Например: <code>165</code>";
            return false;
        }

        if (!TryParseDecimal(matches[0].Value, out var value))
        {
            error = "Не понял число. Отправьте рост в сантиметрах, например <code>165</code>.";
            return false;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded < MinHeightCm || rounded > MaxHeightCm)
        {
            error = $"Рост должен быть от {MinHeightCm} до {MaxHeightCm} см.";
            return false;
        }

        heightCm = rounded;
        return true;
    }

    /// <summary>Разбираю вес в килограммах для расчёта нормы КБЖУ.</summary>
    public static bool TryParseWeightKg(string? input, out decimal weightKg, out string error)
    {
        weightKg = 0m;
        error = string.Empty;

        var matches = NumberRegex().Matches(input ?? string.Empty);
        if (matches.Count != 1)
        {
            error = "Отправьте одно число — вес в килограммах. Например: <code>65</code>";
            return false;
        }

        if (!TryParseDecimal(matches[0].Value, out var value))
        {
            error = "Не понял число. Отправьте вес в килограммах, например <code>65</code>.";
            return false;
        }

        var rounded = Math.Round(value, 1);
        if (rounded < MinWeightKg || rounded > MaxWeightKg)
        {
            error = $"Вес должен быть от {MinWeightKg:0.#} до {MaxWeightKg:0.#} кг.";
            return false;
        }

        weightKg = rounded;
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

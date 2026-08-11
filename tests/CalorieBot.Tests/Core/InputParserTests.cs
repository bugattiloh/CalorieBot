using CalorieBot.Core.Validation;

namespace CalorieBot.Tests.Core;

public class InputParserTests
{
    [Theory]
    [InlineData("Овсянка")]
    [InlineData("A very long but still valid product name up to the limit")]
    [InlineData("ab")]
    public void TryParseProductName_AcceptsValidNames(string input)
    {
        var ok = InputParser.TryParseProductName(input, out var name, out var error);

        Assert.True(ok);
        Assert.Equal(input, name);
        Assert.Empty(error);
    }

    [Fact]
    public void TryParseProductName_CollapsesInternalWhitespaceAndTrims()
    {
        var ok = InputParser.TryParseProductName("  Куриная   грудка  \n\r ", out var name, out _);

        Assert.True(ok);
        Assert.Equal("Куриная грудка", name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    public void TryParseProductName_RejectsTooShort(string? input)
    {
        var ok = InputParser.TryParseProductName(input, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseProductName_RejectsTooLong()
    {
        var tooLong = new string('a', InputParser.MaxNameLength + 1);

        var ok = InputParser.TryParseProductName(tooLong, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseProductName_RejectsLeadingSlash()
    {
        var ok = InputParser.TryParseProductName("/start", out _, out var error);

        Assert.False(ok);
        Assert.Contains("/", error);
    }

    [Fact]
    public void TryParseMacros_ParsesThreeIntegersSeparatedBySpaces()
    {
        var ok = InputParser.TryParseMacros("12 5 1", out var proteins, out var fats, out var carbs, out var error);

        Assert.True(ok);
        Assert.Equal(12m, proteins);
        Assert.Equal(5m, fats);
        Assert.Equal(1m, carbs);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("12.5 5.0 1.2")]
    [InlineData("12,5 5,0 1,2")]
    public void TryParseMacros_AllowsExactlyOneDecimalDigit(string input)
    {
        var ok = InputParser.TryParseMacros(input, out var proteins, out var fats, out var carbs, out var error);

        Assert.True(ok);
        Assert.Equal(12.5m, proteins);
        Assert.Equal(5.0m, fats);
        Assert.Equal(1.2m, carbs);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("12,5 5.0;30")] // старый формат со смешанными разделителями между числами больше не поддерживается
    [InlineData("12.55 5 1")] // два знака после запятой
    [InlineData("-12 5 1")] // отрицательное число
    [InlineData("12 -5 1")]
    [InlineData("12 5 1.")] // точка без цифры после
    public void TryParseMacros_RejectsNonStrictFormat(string input)
    {
        var ok = InputParser.TryParseMacros(input, out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("12 5")]
    [InlineData("12 5 30 40")]
    [InlineData("не число")]
    [InlineData("")]
    public void TryParseMacros_RejectsWhenNotExactlyThreeNumbers(string input)
    {
        var ok = InputParser.TryParseMacros(input, out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseMacros_RejectsAllZero()
    {
        var ok = InputParser.TryParseMacros("0 0 0", out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseMacros_RejectsValueAboveMax()
    {
        var ok = InputParser.TryParseMacros("1001 0 0", out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseCalorieLimit_ParsesNumberIgnoringSpacesAndText()
    {
        var ok = InputParser.TryParseCalorieLimit("2 000 ккал", out var limit, out var error);

        Assert.True(ok);
        Assert.Equal(2000, limit);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(499)]
    [InlineData(10001)]
    public void TryParseCalorieLimit_RejectsOutOfRange(int value)
    {
        var ok = InputParser.TryParseCalorieLimit(value.ToString(), out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(10000)]
    public void TryParseCalorieLimit_AcceptsBoundaryValues(int value)
    {
        var ok = InputParser.TryParseCalorieLimit(value.ToString(), out var limit, out _);

        Assert.True(ok);
        Assert.Equal(value, limit);
    }

    [Fact]
    public void TryParseServingGrams_ParsesPlainNumber()
    {
        var ok = InputParser.TryParseServingGrams("150", out var grams, out var error);

        Assert.True(ok);
        Assert.Equal(150, grams);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5001)]
    public void TryParseServingGrams_RejectsOutOfRange(int value)
    {
        var ok = InputParser.TryParseServingGrams(value.ToString(), out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5000)]
    public void TryParseServingGrams_AcceptsBoundaryValues(int value)
    {
        var ok = InputParser.TryParseServingGrams(value.ToString(), out var grams, out _);

        Assert.True(ok);
        Assert.Equal(value, grams);
    }

    [Fact]
    public void TryParseServingGrams_RejectsWhenNotExactlyOneNumber()
    {
        var ok = InputParser.TryParseServingGrams("150 200", out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseServingSize_EmptyInputIsValidAndNull()
    {
        var ok = InputParser.TryParseServingSize("   ", out var servingSize, out var error);

        Assert.True(ok);
        Assert.Null(servingSize);
        Assert.Empty(error);
    }

    [Fact]
    public void TryParseServingSize_RejectsTooLong()
    {
        var tooLong = new string('x', InputParser.MaxServingSizeLength + 1);

        var ok = InputParser.TryParseServingSize(tooLong, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseServingSize_TrimsAndAccepts()
    {
        var ok = InputParser.TryParseServingSize("  200 г  ", out var servingSize, out _);

        Assert.True(ok);
        Assert.Equal("200 г", servingSize);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(100)]
    public void TryParseAge_AcceptsBoundaryAndTypicalValues(int value)
    {
        var ok = InputParser.TryParseAge(value.ToString(), out var age, out var error);

        Assert.True(ok);
        Assert.Equal(value, age);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(101)]
    public void TryParseAge_RejectsOutOfRange(int value)
    {
        var ok = InputParser.TryParseAge(value.ToString(), out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(165)]
    [InlineData(250)]
    public void TryParseHeightCm_AcceptsBoundaryAndTypicalValues(int value)
    {
        var ok = InputParser.TryParseHeightCm(value.ToString(), out var heightCm, out var error);

        Assert.True(ok);
        Assert.Equal(value, heightCm);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(251)]
    public void TryParseHeightCm_RejectsOutOfRange(int value)
    {
        var ok = InputParser.TryParseHeightCm(value.ToString(), out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParseWeightKg_AcceptsDecimalValue()
    {
        var ok = InputParser.TryParseWeightKg("65.5", out var weightKg, out var error);

        Assert.True(ok);
        Assert.Equal(65.5m, weightKg);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("19")]
    [InlineData("301")]
    public void TryParseWeightKg_RejectsOutOfRange(string value)
    {
        var ok = InputParser.TryParseWeightKg(value, out _, out var error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }
}

using CalorieBot.Core.Time;

namespace CalorieBot.Tests.Core;

public class DayClockTests
{
    private readonly DayClock _clock = new();

    [Fact]
    public void Offset_IsThreeHours()
    {
        Assert.Equal(TimeSpan.FromHours(3), _clock.Offset);
    }

    [Fact]
    public void ToLocal_AddsThreeHoursOffsetToUtc()
    {
        var utc = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);

        var local = _clock.ToLocal(utc);

        Assert.Equal(13, local.Hour);
        Assert.Equal(TimeSpan.FromHours(3), local.Offset);
    }

    [Fact]
    public void ToLocal_RollsOverToNextDay_NearMidnightUtc()
    {
        // 22:00 UTC -> 01:00 следующего дня по UTC+3
        var utc = new DateTime(2026, 8, 8, 22, 0, 0, DateTimeKind.Utc);

        var local = _clock.ToLocal(utc);

        Assert.Equal(9, local.Day);
        Assert.Equal(1, local.Hour);
    }
}

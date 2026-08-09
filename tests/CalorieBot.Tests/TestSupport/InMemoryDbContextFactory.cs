using CalorieBot.Data;
using Microsoft.EntityFrameworkCore;

namespace CalorieBot.Tests.TestSupport;

/// <summary>Изолированный контекст EF Core InMemory — своя база на каждый тест по уникальному имени.</summary>
public static class InMemoryDbContextFactory
{
    public static CalorieBotDbContext Create()
    {
        var options = new DbContextOptionsBuilder<CalorieBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CalorieBotDbContext(options);
    }
}

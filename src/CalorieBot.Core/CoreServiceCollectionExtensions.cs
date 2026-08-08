using CalorieBot.Core.Services;
using CalorieBot.Core.State;
using CalorieBot.Core.Time;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieBot.Core;

/// <summary>Регистрация бизнес-логики.</summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCalorieBotCore(this IServiceCollection services)
    {
        // Кэш нужен и сервисам, и хранилищу состояний диалогов.
        services.AddMemoryCache();

        services.AddSingleton<IDayClock, DayClock>();
        services.AddSingleton<IConversationStateStore, MemoryConversationStateStore>();

        // Сервисы живут в scope апдейта — вместе с DbContext.
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IFavoriteProductService, FavoriteProductService>();
        services.AddScoped<IFoodLogService, FoodLogService>();
        services.AddScoped<IProgressService, ProgressService>();

        return services;
    }
}

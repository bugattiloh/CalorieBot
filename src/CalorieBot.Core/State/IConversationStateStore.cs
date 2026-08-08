using Microsoft.Extensions.Caching.Memory;

namespace CalorieBot.Core.State;

/// <summary>Хранилище состояний диалогов.</summary>
public interface IConversationStateStore
{
    /// <summary>Возвращаю контекст пользователя, создавая его при первом обращении.</summary>
    ConversationContext Get(long userId);

    /// <summary>Продлеваю жизнь контекста после каждого действия пользователя.</summary>
    void Touch(long userId);

    /// <summary>Сбрасываю диалог в исходное состояние.</summary>
    void Clear(long userId);
}

/// <summary>
/// Реализация на IMemoryCache: незавершённые диалоги сами вычищаются через час простоя,
/// так что я не держу мусор по пользователям, которые ушли на середине шага.
/// </summary>
public sealed class MemoryConversationStateStore : IConversationStateStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;

    public MemoryConversationStateStore(IMemoryCache cache) => _cache = cache;

    public ConversationContext Get(long userId) =>
        _cache.GetOrCreate(Key(userId), entry =>
        {
            entry.SlidingExpiration = Lifetime;
            return new ConversationContext();
        })!;

    public void Touch(long userId)
    {
        // Обычного чтения достаточно: IMemoryCache сам продлевает sliding-expiration.
        _ = Get(userId);
    }

    public void Clear(long userId) => _cache.Remove(Key(userId));

    private static string Key(long userId) => $"conversation:{userId}";
}

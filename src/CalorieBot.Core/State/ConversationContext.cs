using CalorieBot.Core.Models;

namespace CalorieBot.Core.State;

/// <summary>
/// Состояние диалога одного пользователя: на каком шаге он находится и что уже успел ввести.
/// Храню в памяти — данные короткоживущие, терять их при рестарте не страшно.
/// </summary>
public sealed class ConversationContext
{
    public ConversationState State { get; set; } = ConversationState.Idle;

    public string? ProductName { get; set; }

    /// <summary>
    /// Пользователь выбрал вводить БЖУ на 100 г, а не на порцию целиком. Пока жду вес порции,
    /// <see cref="Proteins"/>/<see cref="Fats"/>/<see cref="Carbs"/> временно хранят значения именно на 100 г,
    /// а не итоговый черновик — пересчёт происходит в сценарии после ввода веса.
    /// </summary>
    public bool MacrosPerHundredGrams { get; set; }

    public decimal Proteins { get; set; }

    public decimal Fats { get; set; }

    public decimal Carbs { get; set; }

    public int Calories { get; set; }

    public string? ServingSize { get; set; }

    /// <summary>Если продукт пришёл из избранного (или только что там сохранён) — держу его Id.</summary>
    public int? FavoriteProductId { get; set; }

    /// <summary>Id сообщения с инлайн-клавиатурой, которое я редактирую по ходу диалога.</summary>
    public int? ActiveInlineMessageId { get; set; }

    /// <summary>Собираю черновик из накопленного ввода.</summary>
    public ProductDraft ToDraft() => new()
    {
        Name = ProductName ?? string.Empty,
        Proteins = Proteins,
        Fats = Fats,
        Carbs = Carbs,
        ServingSize = ServingSize,
        Calories = Calories
    };

    /// <summary>Заполняю контекст готовым черновиком (например, выбранным из избранного).</summary>
    public void Apply(ProductDraft draft)
    {
        ProductName = draft.Name;
        Proteins = draft.Proteins;
        Fats = draft.Fats;
        Carbs = draft.Carbs;
        ServingSize = draft.ServingSize;
        Calories = draft.Calories;
    }

    /// <summary>Полный сброс: вызываю после завершения сценария и по /start.</summary>
    public void Reset()
    {
        State = ConversationState.Idle;
        ProductName = null;
        MacrosPerHundredGrams = false;
        Proteins = 0m;
        Fats = 0m;
        Carbs = 0m;
        Calories = 0;
        ServingSize = null;
        FavoriteProductId = null;
        ActiveInlineMessageId = null;
    }
}

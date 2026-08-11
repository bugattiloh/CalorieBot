namespace CalorieBot.Data.Entities;

/// <summary>
/// Подкатегория внутри группы «Продукты» (таблица ProductCategories). Четыре базовые заводятся
/// автоматически при первом обращении пользователя к разделу, дальше он может добавлять свои,
/// переименовывать и удалять любые — базовые не исключение.
/// </summary>
public class ProductCategory
{
    public int Id { get; set; }

    public long UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Одна из четырёх заведённых по умолчанию — показываю это только для наглядности, прав не даёт и не отнимает.</summary>
    public bool IsBuiltIn { get; set; }

    public AppUser? User { get; set; }
}

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Группа в списке выбора на карточке подкатегории. Показываются группы того же вида
/// и не служебные; служебная остаётся, только если подкатегория уже в ней. Подходит ли
/// группа по универсальности и записанным операциям, проверяет домен при сохранении.
/// </summary>
/// <param name="Key">Ключ группы.</param>
/// <param name="Name">Название группы.</param>
public sealed record CategoryGroupOption(Guid Key, string Name);

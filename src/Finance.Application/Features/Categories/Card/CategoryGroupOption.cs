namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Группа в списке выбора на карточке подкатегории. Показываются только те,
/// куда перенос вообще возможен: того же вида и не служебные.
/// </summary>
/// <param name="Key">Ключ группы.</param>
/// <param name="Name">Название группы.</param>
public sealed record CategoryGroupOption(Guid Key, string Name);

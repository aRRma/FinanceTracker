using Finance.Domain;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Запись категории: заведение группы, заведение подкатегории, переименование,
/// смена значка и перенос подкатегории в другую группу. Одна команда на все случаи —
/// форма на экране одна и та же, а разделение на «создать» и «изменить» удвоило бы
/// и команду, и обработчик ради одного пустого ключа.
/// </summary>
public sealed record SaveCategoryCommand
{
    /// <summary>Ключ категории. Пуст — категория заводится.</summary>
    public Guid? Key { get; init; }

    /// <summary>
    /// Группа категории. Пусто — речь о группе; заполнено — о подкатегории,
    /// и при правке это же поле задаёт, куда её перенести.
    /// </summary>
    public Guid? ParentKey { get; init; }

    /// <summary>Название категории.</summary>
    public required string Name { get; init; }

    /// <summary>Ключ значка. У новой подкатегории форма подставляет значок группы.</summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Вид заводимой группы. Читается только при заведении группы: у подкатегории
    /// он наследуется, а у существующей группы не меняется никогда.
    /// </summary>
    public CategoryKind? Kind { get; init; }
}

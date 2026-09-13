using Finance.Domain;

namespace Finance.Application.Features.Transactions.Card;

/// <summary>
/// Подкатегория в списке выбора формы. Групп в списке нет — родитель служит
/// подписью: выбрать первый уровень физически невозможно, и правило
/// «операция относится к подкатегории» не нуждается в проверке на экране.
/// </summary>
/// <param name="Key">Ключ подкатегории.</param>
/// <param name="Name">Название подкатегории.</param>
/// <param name="GroupName">Название группы.</param>
/// <param name="Kind">Вид группы — по нему список отбирается под вид операции.</param>
/// <param name="Icon">Ключ значка.</param>
public sealed record CategoryOption(Guid Key, string Name, string GroupName, CategoryKind Kind, string Icon)
{
    /// <summary>Подпись строки выбора: группа и подкатегория.</summary>
    public string Label => $"{GroupName} · {Name}";
}

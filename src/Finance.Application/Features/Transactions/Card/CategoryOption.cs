using Finance.Application.Infrastructure;
using Finance.Domain.Enums;

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
/// <param name="AcceptsAnyKind">Группа принимает операции обоих видов.</param>
/// <param name="Icon">Ключ значка.</param>
/// <param name="Role">Роль подкатегории: у «Прочего» подпись с группой.</param>
public sealed record CategoryOption(
    Guid Key,
    string Name,
    string GroupName,
    CategoryKind Kind,
    bool AcceptsAnyKind,
    string Icon,
    CategoryRole Role)
{
    /// <summary>
    /// Подпись строки выбора: группа и подкатегория.
    /// </summary>
    public string Label => $"{GroupName} · {Name}";

    /// <summary>
    /// Подпись в строке-поле формы: название, а у «Прочего» — с группой.
    /// </summary>
    public string Caption => CategoryCaption.Of(Name, GroupName, Role);

    /// <summary>
    /// Операция такого вида допустима в этой подкатегории. Экран выбора отбирает
    /// строки тем же правилом, и без него выбранная в универсальной группе
    /// подкатегория не нашлась бы в списке формы и молча пропадала.
    /// </summary>
    /// <param name="kind">Вид операции.</param>
    /// <returns><c>true</c>, если вид совпадает или группа универсальна.</returns>
    public bool Accepts(CategoryKind kind) => AcceptsAnyKind || Kind == kind;
}

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>Место в списке выбора: название, чаще всего встречающаяся категория и счётчик.</summary>
/// <param name="Key">Ключ места.</param>
/// <param name="Name">Название.</param>
/// <param name="Caption">Подкатегория, в которой место встречается чаще всего.</param>
/// <param name="Count">Сколько операций записано с этим местом.</param>
/// <param name="IsSelected">Это место и стоит в форме сейчас.</param>
public sealed record PlacePickerRow(Guid Key, string Name, string Caption, int Count, bool IsSelected)
{
    /// <summary>Подпись есть — строка двухстрочная.</summary>
    public bool HasCaption => Caption.Length > 0;
}

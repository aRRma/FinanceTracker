using System.Collections.ObjectModel;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Счета одной валюты в списке выбора. Валюта — заголовок раздела: вместе со счётом
/// меняется валюта суммы, и знать об этом надо до выбора, а не после.
/// </summary>
public sealed class AccountPickerSection : ObservableCollection<AccountPickerRow>
{
    /// <summary>
    /// Создаёт раздел валюты.
    /// </summary>
    /// <param name="title">Заголовок раздела — название валюты.</param>
    /// <param name="rows">Счета этой валюты в порядке показа.</param>
    public AccountPickerSection(string title, IEnumerable<AccountPickerRow> rows)
        : base([.. rows])
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
    }

    /// <summary>
    /// Заголовок раздела.
    /// </summary>
    public string Title { get; }
}

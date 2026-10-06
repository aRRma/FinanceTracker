using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Texts;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Report;

/// <summary>
/// Счёт на экране выбора счетов отчёта: знак, название, подпись и отметка. Отметка
/// меняется на месте, а не заменой строки: список при касании не перестраивается.
/// </summary>
public sealed partial class ReportAccountRow : ObservableObject
{
    /// <summary>
    /// Создаёт строку счёта.
    /// </summary>
    /// <param name="account">Счёт.</param>
    /// <param name="isChecked">Счёт входит в отчёт.</param>
    public ReportAccountRow(ReportAccount account, bool isChecked)
    {
        ArgumentNullException.ThrowIfNull(account);

        Key = account.Key;
        Name = account.Name;
        Color = account.Color;
        Icon = account.Icon;
        Currency = account.Currency;
        IsActive = account.IsActive;
        IsClosed = account.IsClosed;

        // Заблокированный называется прежде накоплений: из употребления выведен
        // и тот и другой, но блокировка объясняет, почему счёта нет в выборе формы
        Caption = account.IsClosed ? UiTexts.AccountClosedLabel
            : account.IsSavings ? UiTexts.PickAccountSavings
            : string.Empty;
        IsChecked = isChecked;
    }

    /// <summary>
    /// Ключ счёта.
    /// </summary>
    public Guid Key { get; }

    /// <summary>
    /// Наименование.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Цвет счёта — заливка знака.
    /// </summary>
    public AccountColor Color { get; }

    /// <summary>
    /// Ключ значка.
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Валюта счёта.
    /// </summary>
    public Currency Currency { get; }

    /// <summary>
    /// Счёт активный — его отмечает «Все активные».
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Счёт заблокирован — знак вполсилы.
    /// </summary>
    public bool IsClosed { get; }

    /// <summary>
    /// Подпись под названием: «Заблокированный», «Накопления» или пусто.
    /// </summary>
    public string Caption { get; }

    /// <summary>
    /// Подпись есть — строка двухстрочная.
    /// </summary>
    public bool HasCaption => Caption.Length > 0;

    /// <summary>
    /// Счёт входит в отчёт.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChecked { get; internal set; }
}

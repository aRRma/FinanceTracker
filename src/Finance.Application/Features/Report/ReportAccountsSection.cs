using System.Collections.ObjectModel;
using System.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Domain.Enums;

namespace Finance.Application.Features.Report;

/// <summary>
/// Счета одной валюты на экране выбора счетов отчёта. Валюта — заголовок раздела:
/// отчёт считается в одной валюте, и границу видно до касания, а не после отказа.
/// </summary>
public sealed class ReportAccountsSection : ObservableCollection<ReportAccountRow>
{
    private static readonly PropertyChangedEventArgs AllActiveChanged = new(nameof(IsAllActive));
    private static readonly PropertyChangedEventArgs OfferChanged = new(nameof(OffersAllActive));

    /// <summary>
    /// Создаёт раздел валюты.
    /// </summary>
    /// <param name="currency">Валюта раздела.</param>
    /// <param name="rows">Счета этой валюты в порядке показа.</param>
    public ReportAccountsSection(Currency currency, IEnumerable<ReportAccountRow> rows)
        : base([.. rows])
    {
        Currency = currency;
        Title = currency.SectionTitle;

        foreach (ReportAccountRow row in this)
        {
            HasActive |= row.IsActive;
        }
    }

    /// <summary>
    /// Валюта раздела.
    /// </summary>
    public Currency Currency { get; }

    /// <summary>
    /// Заголовок раздела — название валюты.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// В валюте есть активные счета — есть что отметить кнопкой «Все активные».
    /// </summary>
    public bool HasActive { get; }

    /// <summary>
    /// Отмечены ровно активные счета этой валюты — вместо кнопки тихая подпись «Выбраны».
    /// </summary>
    public bool IsAllActive
    {
        get;
        internal set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(AllActiveChanged);
            OnPropertyChanged(OfferChanged);
        }
    }

    /// <summary>
    /// Кнопка «Все активные» что-то изменит — она видна.
    /// </summary>
    public bool OffersAllActive => HasActive && !IsAllActive;

    /// <summary>
    /// Отмечен хотя бы один счёт раздела.
    /// </summary>
    internal bool HasChecked
    {
        get
        {
            foreach (ReportAccountRow row in this)
            {
                if (row.IsChecked)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Отмечены ровно активные счета раздела, ни одним больше и ни одним меньше.
    /// </summary>
    internal bool CheckedAreActive()
    {
        if (!HasActive)
        {
            return false;
        }

        foreach (ReportAccountRow row in this)
        {
            if (row.IsChecked != row.IsActive)
            {
                return false;
            }
        }

        return true;
    }
}

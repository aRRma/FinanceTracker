using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Выбор места для формы операции (экран B-07). Частые сверху: справочник
/// наполняется сам и разрастается, а ищут в нём обычно то, чем пользуются.
/// Если совпадения нет, место заводится прямо отсюда — поход в справочник
/// ради одного названия никому не нужен.
/// </summary>
public sealed partial class PlacePickerViewModel : ObservableObject
{
    private readonly IPlacesQuery _places;
    private readonly TransactionPicks _picks;

    private IReadOnlyList<PlaceListItem> _all = [];
    private string _current = string.Empty;

    /// <summary>
    /// Создаёт модель представления выбора места.
    /// </summary>
    /// <param name="places">Справочник мест со счётчиками.</param>
    /// <param name="picks">Куда кладётся выбор для формы операции.</param>
    public PlacePickerViewModel(IPlacesQuery places, TransactionPicks picks)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(picks);

        _places = places;
        _picks = picks;
    }

    /// <summary>
    /// Места, подходящие под набранное.
    /// </summary>
    public ObservableCollection<PlacePickerRow> Rows { get; } = [];

    /// <summary>
    /// Идёт чтение.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Набранное в поиске — оно же имя нового места.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    [NotifyPropertyChangedFor(nameof(CreateCaption))]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Справочник прочитан — до этого пустой список ещё ничего не значит.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// В форме уже стоит место — его можно снять.
    /// </summary>
    [ObservableProperty]
    public partial bool CanClear { get; private set; }

    /// <summary>
    /// Набрано название, которого в справочнике нет, — его предлагается завести.
    /// </summary>
    public bool CanCreate
    {
        get
        {
            string name = Filter.Trim();

            return name.Length > 0
                && !_all.Any(place => string.Equals(place.Name, name, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    /// <summary>
    /// Подпись строки заведения нового места.
    /// </summary>
    public string CreateCaption => $"Создать «{Filter.Trim()}»";

    /// <summary>
    /// Мест нет вовсе, и набрать пока нечего.
    /// </summary>
    public bool IsEmpty => IsLoaded && Rows.Count is 0 && !CanCreate;

    /// <summary>
    /// Читает справочник мест.
    /// </summary>
    /// <param name="current">Место, стоящее в форме сейчас, — оно помечено галочкой.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(string? current, CancellationToken cancellationToken = default)
    {
        _current = current ?? string.Empty;
        CanClear = _current.Length > 0;

        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: следом наполняются
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            _all = await _places.ReadAsync(cancellationToken);

            Rebuild();

            IsLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Запоминает выбранное место.
    /// </summary>
    /// <param name="row">Выбранная строка.</param>
    [RelayCommand]
    public void Pick(PlacePickerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _picks.PlaceName = row.Name;
    }

    /// <summary>
    /// Запоминает набранное как новое место. Самого места здесь не заводится:
    /// оно появится вместе с операцией, и брошенная форма не оставит пустышку
    /// в справочнике.
    /// </summary>
    [RelayCommand]
    public void Create() => _picks.PlaceName = Filter.Trim();

    /// <summary>
    /// Снимает место с операции.
    /// </summary>
    [RelayCommand]
    public void ClearPlace() => _picks.PlaceName = string.Empty;

    /// <summary>
    /// Пересобирает список из прочитанного — в базу за этим не ходят.
    /// </summary>
    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        Rows.Clear();

        string filter = Filter.Trim();

        foreach (PlaceListItem place in _all)
        {
            if (filter.Length > 0 && !place.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            Rows.Add(new PlacePickerRow(
                place.Key,
                place.Name,
                place.TopCategoryName ?? string.Empty,
                place.TransactionCount,
                string.Equals(place.Name, _current, StringComparison.CurrentCultureIgnoreCase)));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}

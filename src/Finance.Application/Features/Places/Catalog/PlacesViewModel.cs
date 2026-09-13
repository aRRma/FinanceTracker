using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Places.Catalog;

/// <summary>
/// Справочник мест: частые сверху, у каждого — число операций и подкатегория,
/// в которой оно встречается чаще всего. Заводить места отсюда нельзя: они
/// появляются сами из формы операции, а сюда приходят наводить порядок.
/// </summary>
public sealed partial class PlacesViewModel : ScreenViewModel
{
    private readonly IPlacesQuery _places;

    private IReadOnlyList<PlaceListItem> _all = [];

    /// <summary>Создаёт модель представления справочника мест.</summary>
    /// <param name="places">Справочник мест со счётчиками.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public PlacesViewModel(IPlacesQuery places, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(places);

        _places = places;
    }

    /// <summary>Места, прошедшие отбор.</summary>
    public ObservableCollection<PlaceListItem> Places { get; } = [];

    /// <summary>Набранные буквы названия. Пусто — показывается весь справочник.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>Идёт чтение.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Справочник пуст вовсе — не отобран до пустоты, а не наполнен ни разу.</summary>
    public bool IsEmpty => _all.Count is 0;

    /// <summary>Отбор не нашёл ни одного места.</summary>
    public bool IsFilteredOut => _all.Count > 0 && Places.Count is 0;

    /// <summary>Перечитывает справочник.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: следом наполняется привязанная
            // коллекция, а её правка вне потока интерфейса роняет разметку
            _all = await _places.ReadAsync(cancellationToken);

            Rebuild();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Пересобирает список под набранные буквы. Из прочитанного, а не из базы:
    /// буквы набирают подряд, и каждая стоила бы запроса со счётом операций.
    /// </summary>
    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        Places.Clear();

        ReadOnlySpan<char> typed = Filter.AsSpan().Trim();

        foreach (PlaceListItem place in _all)
        {
            if (typed.IsEmpty || place.Name.AsSpan().Contains(typed, StringComparison.CurrentCultureIgnoreCase))
            {
                Places.Add(place);
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFilteredOut));
    }

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Places | DataChange.Transactions;

    /// <inheritdoc />
    protected override void Reload() => LoadCommand.Execute(null);
}

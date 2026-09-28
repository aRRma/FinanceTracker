using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    // Номер чтения: жест обновления и перечитывание по чужой правке могут
    // совпасть, и отставшее чтение не должно перекрыть свежее
    private int _generation;

    /// <summary>
    /// Создаёт модель представления справочника мест.
    /// </summary>
    /// <param name="places">Справочник мест со счётчиками.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public PlacesViewModel(IPlacesQuery places, IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(places);

        _places = places;
    }

    /// <summary>
    /// Места, прошедшие отбор.
    /// </summary>
    public ObservableCollection<PlaceListItem> Places { get; } = [];

    /// <summary>
    /// Набранные буквы названия. Пусто — показывается весь справочник.
    /// </summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Справочник прочитан хотя бы раз. Пустой список до чтения значит «ещё
    /// не читали», а не «мест нет», и объяснение про пустой справочник мигнуло бы
    /// на каждом заходе.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Справочник пуст вовсе — не отобран до пустоты, а не наполнен ни разу.
    /// </summary>
    public bool IsEmpty => IsLoaded && _all.Count is 0;

    /// <summary>
    /// Отбор не нашёл ни одного места.
    /// </summary>
    public bool IsFilteredOut => _all.Count > 0 && Places.Count is 0;

    /// <summary>
    /// Перечитывает справочник.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        int generation = ++_generation;

        // ConfigureAwait(false) здесь недопустим: следом наполняется привязанная
        // коллекция, а её правка вне потока интерфейса роняет разметку
        IReadOnlyList<PlaceListItem> all = await _places.ReadAsync(cancellationToken);

        if (generation != _generation)
        {
            return;
        }

        _all = all;

        Rebuild();

        // Не в Rebuild: его же зовёт набор букв в поиске, а он о чтении ничего не говорит
        IsLoaded = true;
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
    protected override Task ReloadAsync() => LoadAsync();
}

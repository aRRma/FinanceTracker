using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Settings.TimeZones;

/// <summary>
/// Экран часового пояса. Зон в системе сотни, поэтому список отбирается по
/// набранным буквам; первой строкой — возврат к системному поясу, иначе после
/// ручного выбора вернуться к нему было бы нечем.
/// </summary>
public sealed partial class TimeZoneViewModel : ScreenViewModel
{
    private readonly ISettingsSummaryQuery _summary;
    private readonly IChangeTimeZoneHandler _change;
    private readonly IClock _clock;

    private IReadOnlyList<TimeZoneInfo> _all = [];

    /// <summary>Создаёт модель представления экрана часового пояса.</summary>
    /// <param name="summary">Состояние настроек.</param>
    /// <param name="change">Выбор пояса.</param>
    /// <param name="clock">Часы приложения: у них действующий пояс и текущий момент.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public TimeZoneViewModel(
        ISettingsSummaryQuery summary,
        IChangeTimeZoneHandler change,
        IClock clock,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(clock);

        _summary = summary;
        _change = change;
        _clock = clock;
    }

    /// <summary>Зоны, прошедшие отбор. Первой строкой — «Как в системе».</summary>
    public ObservableCollection<TimeZoneOption> Zones { get; } = [];

    /// <summary>Набранные буквы идентификатора. Пусто — показываются все зоны.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>Пояс взят из системы — ручного выбора не было.</summary>
    [ObservableProperty]
    public partial bool IsFromSystem { get; private set; } = true;

    /// <summary>Отбор не нашёл ни одной зоны.</summary>
    [ObservableProperty]
    public partial bool IsFilteredOut { get; private set; }

    /// <summary>Перечитывает список зон и текущий выбор.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом наполняется привязанная
        // коллекция, а её правка вне потока интерфейса роняет разметку
        SettingsSummary summary = await _summary.ReadAsync(cancellationToken);

        IsFromSystem = summary.TimeZoneFromSystem;

        // Список зон читается у системы один раз на заход: он не меняется, пока
        // приложение живёт, а перебор шестисот зон на каждую набранную букву
        // сделал бы поиск заметно медленным.
        // Порядок задаётся заново: система сортирует по зимнему смещению, а на
        // экране стоит действующее, и на летнем времени список выглядел бы вразнобой
        DateTimeOffset now = _clock.NowUtc;

        _all =
        [
            .. TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(zone => zone.GetUtcOffset(now))
                .ThenBy(zone => zone.Id, StringComparer.Ordinal)
        ];

        Rebuild();
    }

    /// <summary>Ставит выбранную зону.</summary>
    /// <param name="option">Строка списка. У «Как в системе» идентификатора нет.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task SelectAsync(TimeZoneOption option, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(option);

        await _change.HandleAsync(option.Id, cancellationToken);

        IsFromSystem = option.Id is null;

        Rebuild();
    }

    /// <summary>
    /// Пересобирает список под набранные буквы. Из прочитанного, а не у системы:
    /// буквы набирают подряд, и каждая стоила бы полного перебора зон.
    /// </summary>
    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        Zones.Clear();

        string current = _clock.TimeZone.Id;

        Zones.Add(new TimeZoneOption
        {
            Id = null,
            Caption = "Как в системе",
            Offset = Offset(TimeZoneInfo.Local),
            IsSelected = IsFromSystem
        });

        ReadOnlySpan<char> typed = Filter.AsSpan().Trim();
        int found = 0;

        foreach (TimeZoneInfo zone in _all)
        {
            if (!typed.IsEmpty && !zone.Id.AsSpan().Contains(typed, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            found++;

            Zones.Add(new TimeZoneOption
            {
                Id = zone.Id,
                Caption = zone.Id,
                Offset = Offset(zone),
                IsSelected = !IsFromSystem && string.Equals(zone.Id, current, StringComparison.Ordinal)
            });
        }

        IsFilteredOut = found is 0;
    }

    /// <summary>Смещение зоны от UTC на сегодня — по нему зону и узнают.</summary>
    private string Offset(TimeZoneInfo zone)
    {
        TimeSpan offset = zone.GetUtcOffset(_clock.NowUtc);
        char sign = offset < TimeSpan.Zero ? '-' : '+';

        return string.Create(
            CultureInfo.InvariantCulture,
            $"UTC{sign}{offset.Duration():hh\\:mm}");
    }

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Settings;

    /// <inheritdoc />
    protected override void Reload() => LoadCommand.Execute(null);
}

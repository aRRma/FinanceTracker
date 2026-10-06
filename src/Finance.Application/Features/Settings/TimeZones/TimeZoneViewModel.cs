using Finance.Application.Texts;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Settings.TimeZones;

/// <summary>
/// Экран часового пояса. Зон в системе сотни, и большинство — дубли и служебные,
/// поэтому список короткий: по строке на смещение, подписанной городами.
/// Первой строкой — возврат к системному поясу, иначе после ручного выбора
/// вернуться к нему было бы нечем.
/// </summary>
/// <remarks>
/// Строка ставит одну зону своего смещения — первую по приоритету. Тому, у кого
/// переход на летнее время идёт не как у неё, после перехода придётся выбрать
/// заново: это цена короткого списка.
/// </remarks>
public sealed partial class TimeZoneViewModel : ScreenViewModel
{
    // Городов в подписи строки не больше трёх: длиннее она уходила бы на третью
    // строку, а узнают смещение и по первым городам
    private const int CitiesInCaption = 3;

    private readonly ISettingsSummaryQuery _summary;
    private readonly IChangeTimeZoneHandler _change;
    private readonly IClock _clock;

    // Строки собираются при чтении, а выбор только переставляет отметку:
    // подписи и смещения до следующего чтения не меняются
    private IReadOnlyList<(TimeSpan Offset, string[] Ids, TimeZoneOption Row)> _rows = [];

    /// <summary>
    /// Создаёт модель представления экрана часового пояса.
    /// </summary>
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

    /// <summary>
    /// Строки списка по возрастанию смещения. Первой строкой — «Как в системе».
    /// </summary>
    public ObservableCollection<TimeZoneOption> Zones { get; } = [];

    /// <summary>
    /// Пояс взят из системы — ручного выбора не было.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFromSystem { get; private set; } = true;

    /// <summary>
    /// Перечитывает список и текущий выбор.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом наполняется привязанная
        // коллекция, а её правка вне потока интерфейса роняет разметку
        SettingsSummary summary = await _summary.ReadAsync(cancellationToken);

        IsFromSystem = summary.TimeZoneFromSystem;

        // Смещение — на сегодня: зимой и летом города сходятся в строки по-разному,
        // и постоянное смещение полгода называло бы не то время.
        // Зону, которой нет в системе устройства, строка не предлагает:
        // выбор её отверг бы обработчик
        DateTimeOffset now = _clock.NowUtc;

        _rows =
        [
            .. TimeZoneCities.Ids
                .Select(static id => TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone) ? zone : null)
                .OfType<TimeZoneInfo>()
                .GroupBy(zone => zone.GetUtcOffset(now))
                .OrderBy(static bucket => bucket.Key)
                .Select(static bucket => Row(bucket.Key, [.. bucket.Select(static zone => zone.Id)]))
        ];

        Rebuild();
    }

    /// <summary>
    /// Ставит пояс выбранной строки.
    /// </summary>
    /// <param name="option">Строка списка. У «Как в системе» идентификатора нет.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task SelectAsync(TimeZoneOption option, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(option);

        await _change.HandleAsync(option.Id, cancellationToken);

        IsFromSystem = option.Id is null;

        Rebuild();
    }

    private void Rebuild()
    {
        Zones.Clear();

        Zones.Add(new TimeZoneOption
        {
            Id = null,
            Caption = UiTexts.TimeZoneSystem,
            Offset = Offset(TimeZoneInfo.Local.GetUtcOffset(_clock.NowUtc)),
            IsSelected = IsFromSystem
        });

        // Зона, выбранная до короткого списка, могла в нём не остаться — тогда
        // отмечена строка того же смещения: сегодня время у них одно
        TimeZoneInfo current = _clock.TimeZone;
        bool listed = _rows.Any(row => row.Ids.Contains(current.Id, StringComparer.Ordinal));
        TimeSpan currentOffset = current.GetUtcOffset(_clock.NowUtc);

        foreach ((TimeSpan offset, string[] ids, TimeZoneOption row) in _rows)
        {
            bool chosen = listed ? ids.Contains(current.Id, StringComparer.Ordinal) : offset == currentOffset;

            Zones.Add(row with { IsSelected = !IsFromSystem && chosen });
        }
    }

    /// <summary>
    /// Строка смещения: ставит первую по приоритету зону, подписана первыми городами.
    /// </summary>
    private static (TimeSpan, string[], TimeZoneOption) Row(TimeSpan offset, string[] ids) =>
        (offset, ids, new TimeZoneOption
        {
            Id = ids[0],
            Caption = string.Join(", ", ids.Take(CitiesInCaption).Select(CityNames.Of)),
            Offset = Offset(offset),
            IsSelected = false
        });

    /// <summary>
    /// Смещение от UTC коротко: «UTC+3», «UTC+5:30», «UTC−3:30». Минуты — только
    /// у зон, где они есть: нули в каждой строке читались бы шумом.
    /// </summary>
    private static string Offset(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
        {
            return "UTC";
        }

        // Типографский минус: дефис рядом с цифрой читается тире
        char sign = offset < TimeSpan.Zero ? '−' : '+';
        TimeSpan size = offset.Duration();

        return size.Minutes is 0
            ? string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{size.Hours}")
            : string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{size.Hours}:{size.Minutes:00}");
    }

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Settings;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}

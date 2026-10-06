using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Application.Texts;

namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Экран «Отчёты о сбоях»: список от новых к старым, отчёт целиком по касанию, отправка файлом и очистка.
/// </summary>
/// <remarks>
/// Отчёты лежат не в базе, а в файле, и изменения базы их не касаются: экран читает их при появлении,
/// а не по оповещению. Новый отчёт, пока экран открыт, приходит только вместе со сбоем — и экран при этом
/// показывает не его.
/// </remarks>
public sealed partial class CrashReportsViewModel : ObservableObject
{
    /// <summary>
    /// Имя сведённого файла для «Поделиться»: под ним он придёт адресату.
    /// </summary>
    private const string ShareFileName = "finance-crash-reports.txt";

    private readonly CrashReports _reports;
    private readonly IClock _clock;

    /// <summary>
    /// Модель экрана.
    /// </summary>
    /// <param name="reports">Отчёты о сбоях.</param>
    /// <param name="clock">Часы — для зоны и «сегодня» в подписи момента.</param>
    public CrashReportsViewModel(CrashReports reports, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(clock);

        _reports = reports;
        _clock = clock;
    }

    /// <summary>
    /// Отчёты от новых к старым.
    /// </summary>
    public ObservableCollection<CrashReportItem> Items { get; } = [];

    /// <summary>
    /// Первое чтение прошло — без этого пустое состояние мигнуло бы до него.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// Отчёты есть — «Поделиться» и «Очистить» доступны.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasReports { get; private set; }

    /// <summary>
    /// Прочитано, и отчётов нет.
    /// </summary>
    public bool IsEmpty => IsLoaded && !HasReports;

    /// <summary>
    /// Читает отчёты.
    /// </summary>
    /// <param name="cancellationToken">Отмена.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом меняется привязанный список
        IReadOnlyList<CrashReport> reports = await _reports.ReadAsync(cancellationToken);

        Items.Clear();

        foreach (CrashReport report in reports)
        {
            Items.Add(new CrashReportItem
            {
                Kind = KindOf(report.Kind),
                Caption = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateText.Moment(report.AtUtc, _clock.TimeZone, _clock.Today)} · {report.Headline}"),
                Text = report.Text,
            });
        }

        HasReports = Items.Count > 0;
        IsLoaded = true;
    }

    /// <summary>
    /// Раскрывает отчёт или сворачивает раскрытый.
    /// </summary>
    /// <param name="item">Строка.</param>
    public static void Toggle(CrashReportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.IsExpanded = !item.IsExpanded;
    }

    /// <summary>
    /// Готовит файл для «Поделиться»: оба файла отчётов, сведённые в один.
    /// </summary>
    /// <param name="folder">Папка временных файлов.</param>
    /// <param name="cancellationToken">Отмена до начала копирования.</param>
    /// <returns>Путь к сведённому файлу.</returns>
    public Task<string> PrepareShareAsync(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        string path = Path.Combine(folder, ShareFileName);

        return Task.Run(
            () =>
            {
                Directory.CreateDirectory(folder);
                _reports.CopyTo(path);

                return path;
            },
            cancellationToken);
    }

    /// <summary>
    /// Удаляет все отчёты и сведённую для отправки копию. Вопрос задаёт страница.
    /// </summary>
    /// <param name="shareFolder">Папка временных файлов, куда кладётся копия для «Поделиться».</param>
    /// <param name="cancellationToken">Отмена до начала удаления.</param>
    /// <remarks>
    /// Удаление — в пуле потоков: оно ждёт блокировку записи. Список перечитывается и после неудачи —
    /// удалился один файл из двух, и экран обязан показать то, что осталось, а не прежний список.
    /// Копия удаляется тоже: «Очистить» обещает, что отчётов на телефоне нет, а сразу после отправки
    /// её не удалить — приложение-адресат читает файл позже.
    /// </remarks>
    public async Task ClearAsync(string shareFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shareFolder);

        try
        {
            await Task.Run(
                () =>
                {
                    _reports.Clear();
                    File.Delete(Path.Combine(shareFolder, ShareFileName));
                },
                cancellationToken);
        }
        finally
        {
            await LoadAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Вид сбоя словом. Падение .NET и падение Java для того, кто читает список, — одно: приложение закрылось.
    /// </summary>
    private static string KindOf(CrashKind kind) => kind switch
    {
        CrashKind.Unhandled or CrashKind.Java => UiTexts.CrashKindFatal,
        CrashKind.Warning => UiTexts.CrashKindWarning,
        CrashKind.ExitReason => UiTexts.CrashKindExitReason,
        _ => UiTexts.CrashKindCaught,
    };
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Application.Texts;

namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Экран отчёта о сбое: что и где случилось, версия и телефон, причина со стеком и шкала действий
/// перед сбоем; «Поделиться» отдаёт этот отчёт.
/// </summary>
/// <remarks>
/// Стек свёрнут до верхних строк: причина видна в начале, а полный стек — по касанию. Время следа
/// в файле — UTC, на экране — время пользователя, как и подпись момента.
/// </remarks>
public sealed partial class CrashReportViewModel : ObservableObject
{
    /// <summary>
    /// Имя файла одного отчёта для «Поделиться».
    /// </summary>
    internal const string ShareFileName = "finance-crash-report.txt";

    /// <summary>
    /// Сколько строк стека видно свёрнутым.
    /// </summary>
    internal const int PreviewLines = 5;

    /// <summary>
    /// Обработчик появления экрана в следе: общий у всех страниц, читающих данные.
    /// </summary>
    private const string AppearingHandler = "DataPage.OnAppearing";

    /// <summary>
    /// Начала служебных строк отчёта о прошлом завершении.
    /// </summary>
    private static readonly string[] ExitServiceLines = ["exit ", "description ", "screen "];

    private readonly CrashReportChoice _choice;
    private readonly IClock _clock;
    private CrashReport? _report;
    private string[] _lines = [];
    private string _stack = string.Empty;
    private string _preview = string.Empty;

    /// <summary>
    /// Модель экрана.
    /// </summary>
    /// <param name="choice">Отчёт, выбранный в списке.</param>
    /// <param name="clock">Часы — для зоны и «сегодня».</param>
    public CrashReportViewModel(CrashReportChoice choice, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(clock);

        _choice = choice;
        _clock = clock;
    }

    /// <summary>
    /// Вид сбоя — по нему значок.
    /// </summary>
    [ObservableProperty]
    public partial CrashCategory Category { get; private set; }

    /// <summary>
    /// Что случилось словами.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Когда и на каком экране.
    /// </summary>
    [ObservableProperty]
    public partial string Caption { get; private set; } = string.Empty;

    /// <summary>
    /// Версия и сборка приложения.
    /// </summary>
    [ObservableProperty]
    public partial string Version { get; private set; } = string.Empty;

    /// <summary>
    /// Производитель и модель телефона.
    /// </summary>
    [ObservableProperty]
    public partial string Phone { get; private set; } = string.Empty;

    /// <summary>
    /// Версия Android.
    /// </summary>
    [ObservableProperty]
    public partial string Android { get; private set; } = string.Empty;

    /// <summary>
    /// Короткая причина над стеком.
    /// </summary>
    [ObservableProperty]
    public partial string Cause { get; private set; } = string.Empty;

    /// <summary>
    /// Стек: свёрнутый — верхние строки, раскрытый — целиком.
    /// </summary>
    [ObservableProperty]
    public partial string Stack { get; private set; } = string.Empty;

    /// <summary>
    /// Стек длиннее свёрнутого — касание его раскрывает.
    /// </summary>
    [ObservableProperty]
    public partial bool CanExpandStack { get; private set; }

    /// <summary>
    /// Подпись под стеком: «Весь стек — 10 строк» или «Свернуть».
    /// </summary>
    [ObservableProperty]
    public partial string StackToggle { get; private set; } = string.Empty;

    /// <summary>
    /// Шкала действий перед сбоем, последней строкой — сам сбой.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrail))]
    public partial IReadOnlyList<CrashTrailEntry> Trail { get; private set; } = [];

    /// <summary>
    /// След есть: у прошлого завершения его нет — тот процесс уже мёртв.
    /// </summary>
    public bool HasTrail => Trail.Count > 0;

    /// <summary>
    /// Стек раскрыт.
    /// </summary>
    public bool IsStackExpanded { get; private set; }

    /// <summary>
    /// Показывает отчёт, выбранный в списке.
    /// </summary>
    public void Load()
    {
        if (_choice.Current is not { } report)
        {
            return;
        }

        _report = report;
        CrashReportSummary summary = CrashReportSummary.Of(report);

        Category = summary.Category;
        Title = summary.Title;
        Caption = CrashReportsViewModel.Join(Moment(report.AtUtc), CrashReportsViewModel.ScreenText(summary.Screen));
        Version = summary.Version;
        Phone = summary.Phone;
        Android = summary.Android;
        Cause = summary.Detail.Length > 0 ? summary.Detail : report.Headline;

        // Служебные строки прошлого завершения — причина, описание, экран — уже сказаны шапкой и съели
        // бы свёрнутый стек; табуляция Java на узком экране съедает ширину, отступ вызова — два пробела
        _lines =
        [
            .. report.Description
                .Where(line => report.Kind is not CrashKind.ExitReason || !ExitServiceLines.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal)))
                .Select(static line => line.Replace("\t", "  ", StringComparison.Ordinal)),
        ];
        _stack = string.Join('\n', _lines);
        _preview = string.Join('\n', _lines.Take(PreviewLines));
        CanExpandStack = _lines.Length > PreviewLines;
        IsStackExpanded = false;
        Show();

        Trail = TrailOf(report);
    }

    /// <summary>
    /// Раскрывает стек целиком или сворачивает до верхних строк.
    /// </summary>
    public void ToggleStack()
    {
        if (!CanExpandStack)
        {
            return;
        }

        IsStackExpanded = !IsStackExpanded;
        Show();
    }

    /// <summary>
    /// Кладёт этот отчёт в отдельный файл для «Поделиться».
    /// </summary>
    /// <param name="folder">Папка временных файлов.</param>
    /// <param name="cancellationToken">Отмена до начала записи.</param>
    /// <returns>Путь к файлу; пусто — отчёт не выбран.</returns>
    public Task<string?> PrepareShareAsync(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        if (_report is not { } report)
        {
            return Task.FromResult<string?>(null);
        }

        string path = Path.Combine(folder, ShareFileName);

        return Task.Run<string?>(
            () =>
            {
                Directory.CreateDirectory(folder);
                CrashReports.CopyTo(report, path);

                return path;
            },
            cancellationToken);
    }

    private void Show()
    {
        Stack = IsStackExpanded || !CanExpandStack ? _stack : _preview;
        StackToggle = !CanExpandStack ? string.Empty
            : IsStackExpanded ? UiTexts.CrashReportStackCollapse
            : string.Format(
                UiCulture.Current,
                UiTexts.CrashReportStackAll,
                Plural.Of(_lines.Length, UiTexts.CrashReportStackLinesOne, UiTexts.CrashReportStackLinesFew, UiTexts.CrashReportStackLinesMany));
    }

    private CrashTrailEntry[] TrailOf(CrashReport report)
    {
        if (report.Trail.Count is 0)
        {
            return [];
        }

        List<CrashTrailEntry> entries = [];

        foreach (string raw in report.Trail)
        {
            if (CrashReportSummary.TrailLine().Match(raw) is not { Success: true } line)
            {
                continue;
            }

            string value = line.Groups["value"].Value;

            // Появление экрана приходит перед каждым переходом и повторяет его; в файле оно остаётся
            if (string.Equals(value, AppearingHandler, StringComparison.Ordinal))
            {
                continue;
            }

            (string text, bool code) = line.Groups["kind"].Value switch
            {
                "nav" => CrashScreens.Of(value) is { } screen ? (CrashReportsViewModel.ScreenText(screen), false) : (value, true),
                "life" => (LifeText(value), false),
                "rule" => (string.Format(UiCulture.Current, UiTexts.CrashTrailRule, value), false),
                _ => (value, true),
            };

            entries.Add(new CrashTrailEntry { Time = LocalTime(report.AtUtc, line.Groups["time"].Value), Text = text, IsCode = code });
        }

        entries.Add(new CrashTrailEntry { Time = ClockText(report.AtUtc), Text = UiTexts.CrashTrailCrash, IsCrash = true });

        return [.. entries];
    }

    private static string LifeText(string value) =>
        Enum.TryParse(value, ignoreCase: false, out LifecycleEvent lifecycle) ? lifecycle switch
        {
            LifecycleEvent.Started => UiTexts.CrashTrailStarted,
            LifecycleEvent.Stopped => UiTexts.CrashTrailStopped,
            LifecycleEvent.Restarted => UiTexts.CrashTrailRestarted,
            LifecycleEvent.ThemeChanged => UiTexts.CrashTrailThemeChanged,
            LifecycleEvent.TrimMemory => UiTexts.CrashTrailTrimMemory,
            _ => value,
        } : value;

    /// <summary>
    /// Время следа в зоне пользователя. В файле у события только время суток UTC; дата берётся у отчёта —
    /// на экране всё равно только время, а сдвиг зоны от даты зависит лишь на переходе на летнее время.
    /// </summary>
    private string LocalTime(DateTimeOffset atUtc, string time) =>
        TimeSpan.TryParseExact(time, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out TimeSpan of)
            ? ClockText(new DateTimeOffset(atUtc.UtcDateTime.Date, TimeSpan.Zero) + of)
            : time;

    private string ClockText(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, _clock.TimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private string Moment(DateTimeOffset atUtc) => DateText.Moment(atUtc, _clock.TimeZone, _clock.Today);
}

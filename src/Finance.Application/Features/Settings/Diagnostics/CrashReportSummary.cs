using System.Globalization;
using System.Text.RegularExpressions;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Application.Texts;

namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Отчёт о сбое, разложенный для экрана: вид, заголовок, короткая причина, экран, версия и телефон.
/// </summary>
/// <remarks>
/// Файл отчётов пишется для разбора, а не для глаз: тип с пространством имён, причина завершения именем
/// члена, адрес вместо экрана. Здесь это превращается в слова; сам файл и «Поделиться» остаются как были.
/// </remarks>
internal sealed partial class CrashReportSummary
{
    /// <summary>
    /// Начало строки первого вложенного исключения в описании сбоя.
    /// </summary>
    private const string InnerPrefix = "--- inner 1: ";

    private CrashReportSummary(CrashReport report)
    {
        Report = report;
        (Category, ExitReason? reason, int status) = Classify(report);
        Detail = DetailOf(report, Category, reason, status);
        Screen = ScreenOf(report) is { } location ? CrashScreens.Of(location) : null;

        Match device = DeviceLine().Match(report.Device);
        Version = device.Success ? device.Groups["version"].Value : report.Device;
        Phone = device.Success ? device.Groups["model"].Value : "";
        Android = device.Success ? device.Groups["android"].Value : "";
    }

    /// <summary>
    /// Отчёт, из которого всё собрано.
    /// </summary>
    internal CrashReport Report { get; }

    /// <summary>
    /// Вид сбоя.
    /// </summary>
    internal CrashCategory Category { get; }

    /// <summary>
    /// Короткая причина: тип исключения без пространства имён, сигнал, причина убийства; пусто — сказать нечего.
    /// </summary>
    internal string Detail { get; }

    /// <summary>
    /// Экран, на котором случился сбой; пусто — не известен.
    /// </summary>
    internal string? Screen { get; }

    /// <summary>
    /// Версия и сборка приложения: <c>1.0.4 (10004)</c>.
    /// </summary>
    internal string Version { get; }

    /// <summary>
    /// Производитель и модель телефона.
    /// </summary>
    internal string Phone { get; }

    /// <summary>
    /// Версия Android.
    /// </summary>
    internal string Android { get; }

    /// <summary>
    /// Заголовок по виду сбоя.
    /// </summary>
    internal string Title => TitleOf(Category);

    /// <summary>
    /// Раскладывает отчёт.
    /// </summary>
    /// <param name="report">Отчёт.</param>
    internal static CrashReportSummary Of(CrashReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new CrashReportSummary(report);
    }

    /// <summary>
    /// Заголовок вида сбоя.
    /// </summary>
    /// <param name="category">Вид.</param>
    internal static string TitleOf(CrashCategory category) => category switch
    {
        CrashCategory.AppClosed => UiTexts.CrashCategoryAppClosed,
        CrashCategory.SystemFault => UiTexts.CrashCategorySystemFault,
        CrashCategory.Froze => UiTexts.CrashCategoryFroze,
        CrashCategory.Killed => UiTexts.CrashCategoryKilled,
        CrashCategory.Warning => UiTexts.CrashCategoryWarning,
        _ => UiTexts.CrashCategoryActionFailed,
    };

    /// <summary>
    /// Короткое имя типа: <c>CrashedByAdbException</c> из <c>android.app.RemoteServiceException$CrashedByAdbException</c>.
    /// </summary>
    /// <param name="headline">Первая строка сбоя; у типов из белого списка за двоеточием — текст.</param>
    internal static string ShortType(string headline)
    {
        string type = headline.Split(':', 2)[0].Trim();

        return type[(type.LastIndexOfAny(['.', '$']) + 1)..];
    }

    private static (CrashCategory Category, ExitReason? Reason, int Status) Classify(CrashReport report)
    {
        if (report.Kind is not CrashKind.ExitReason)
        {
            CrashCategory category = report.Kind switch
            {
                CrashKind.Unhandled or CrashKind.Java => CrashCategory.AppClosed,
                CrashKind.Warning => CrashCategory.Warning,
                _ => CrashCategory.ActionFailed,
            };

            return (category, null, 0);
        }

        // Первая строка отчёта о прошлом завершении — «exit CrashNative status 11»
        Match exit = ExitLine().Match(report.Headline);

        if (!exit.Success || !Enum.TryParse(exit.Groups["reason"].Value, ignoreCase: false, out ExitReason reason))
        {
            return (CrashCategory.Killed, null, 0);
        }

        int status = int.Parse(exit.Groups["status"].Value, CultureInfo.InvariantCulture);

        return (reason switch
        {
            ExitReason.Crash or ExitReason.ExitSelf => CrashCategory.AppClosed,
            ExitReason.CrashNative => CrashCategory.SystemFault,
            ExitReason.Anr => CrashCategory.Froze,
            _ => CrashCategory.Killed,
        }, reason, status);
    }

    private static string DetailOf(CrashReport report, CrashCategory category, ExitReason? reason, int status)
    {
        if (reason is null)
        {
            // Составное исключение — обёртка: причина в первом вложенном, его и называем
            string inner = report.Description.FirstOrDefault(static line => line.StartsWith(InnerPrefix, StringComparison.Ordinal)) ?? "";

            return report.Headline.Length is 0 ? ""
                : ShortType(report.Headline) is "AggregateException" && inner.Length > 0 ? ShortType(inner[InnerPrefix.Length..])
                : ShortType(report.Headline);
        }

        return category switch
        {
            // «signal 11 (SIGSEGV), code 0 (SI_USER)» — имя сигнала скажет больше номера
            CrashCategory.SystemFault => report.Description.Select(static line => SignalLine().Match(line))
                .FirstOrDefault(static match => match.Success)?.Groups["name"].Value ?? "",
            CrashCategory.Killed => reason switch
            {
                ExitReason.LowMemory => UiTexts.CrashKilledLowMemory,
                ExitReason.Freezer => UiTexts.CrashKilledFreezer,
                ExitReason.ExcessiveResourceUsage => UiTexts.CrashKilledResources,
                ExitReason.InitializationFailure => UiTexts.CrashKilledInitialization,
                ExitReason.Signaled => string.Format(UiCulture.Current, UiTexts.CrashKilledSignal, status),
                _ => "",
            },
            _ => "",
        };
    }

    /// <summary>
    /// Адрес экрана: у отчёта обработчика — последний переход следа, у прошлого завершения — сводка от системы.
    /// </summary>
    private static string? ScreenOf(CrashReport report)
    {
        for (int index = report.Trail.Count - 1; index >= 0; index--)
        {
            if (TrailLine().Match(report.Trail[index]) is { Success: true } line && line.Groups["kind"].Value is "nav")
            {
                return line.Groups["value"].Value;
            }
        }

        // «screen //report | ReportPage.OnRefreshing» — экран до черты, после неё последнее действие
        return report.Description.FirstOrDefault(static line => line.StartsWith("screen ", StringComparison.Ordinal))
            ?["screen ".Length..].Split(" | ", 2)[0];
    }

    /// <summary>
    /// Строка следа: время, вид события и значение.
    /// </summary>
    [GeneratedRegex(@"^(?<time>\d{2}:\d{2}:\d{2}(\.\d+)?) (?<kind>\w+) (?<value>.*)$")]
    internal static partial Regex TrailLine();

    [GeneratedRegex(@"^app (?<version>.+), android (?<android>[^,]+), (?<model>.+)$")]
    private static partial Regex DeviceLine();

    [GeneratedRegex(@"^exit (?<reason>\w+) status (?<status>-?\d+)$")]
    private static partial Regex ExitLine();

    [GeneratedRegex(@"^signal -?\d+ \((?<name>[^)]*)\)")]
    private static partial Regex SignalLine();
}

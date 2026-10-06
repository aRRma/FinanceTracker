using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Settings.About;

/// <summary>
/// Экран «О программе»: версия приложения и номер схемы базы. Номер схемы нужен
/// не из любопытства — по нему сверяют, накатилась ли миграция после обновления.
/// </summary>
/// <remarks>
/// Наследовать <c>ScreenViewModel</c> незачем: ни версия, ни схема не меняются,
/// пока приложение открыто, и подписываться на изменения данных нечему. Число
/// отчётов о сбоях перечитывается при каждом появлении — после очистки на экране
/// отчётов строка обязана показать ноль.
/// </remarks>
public sealed partial class AboutViewModel : ObservableObject
{
    private readonly ISettingsSummaryQuery _summary;
    private readonly CrashReports _reports;

    /// <summary>
    /// Создаёт модель представления экрана «О программе».
    /// </summary>
    /// <param name="summary">Состояние настроек.</param>
    /// <param name="reports">Отчёты о сбоях — для их числа в строке.</param>
    public AboutViewModel(ISettingsSummaryQuery summary, CrashReports reports)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(reports);

        _summary = summary;
        _reports = reports;
    }

    /// <summary>
    /// Сколько отчётов о сбоях лежит на телефоне.
    /// </summary>
    [ObservableProperty]
    public partial string CrashReportCount { get; private set; } = string.Empty;

    /// <summary>
    /// Версия приложения.
    /// </summary>
    [ObservableProperty]
    public partial string Version { get; private set; } = string.Empty;

    /// <summary>
    /// Номер схемы базы — число накатанных миграций.
    /// </summary>
    [ObservableProperty]
    public partial string Schema { get; private set; } = string.Empty;

    /// <summary>
    /// Читает сведения о программе.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
        // свойства, а их правка вне потока интерфейса роняет разметку
        SettingsSummary summary = await _summary.ReadAsync(cancellationToken);

        Version = summary.Version;
        Schema = summary.Schema.ToString(CultureInfo.InvariantCulture);
        // Отчёты — побочная строка: не прочитался их файл — без числа, но версия и схема остаются на экране
        try
        {
            CrashReportCount = (await _reports.ReadAsync(cancellationToken)).Count.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            CrashReportCount = string.Empty;
        }
    }
}

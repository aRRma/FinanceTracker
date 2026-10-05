using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Settings.About;

/// <summary>
/// Экран «О программе»: версия приложения и номер схемы базы. Номер схемы нужен
/// не из любопытства — по нему сверяют, накатилась ли миграция после обновления.
/// </summary>
/// <remarks>
/// Наследовать <c>ScreenViewModel</c> незачем: ни версия, ни схема не меняются,
/// пока приложение открыто, и подписываться на изменения данных нечему.
/// </remarks>
public sealed partial class AboutViewModel : ObservableObject
{
    private readonly ISettingsSummaryQuery _summary;

    /// <summary>
    /// Создаёт модель представления экрана «О программе».
    /// </summary>
    /// <param name="summary">Состояние настроек.</param>
    public AboutViewModel(ISettingsSummaryQuery summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        _summary = summary;
    }

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
    }
}

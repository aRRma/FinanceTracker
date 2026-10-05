using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Читает состояние настроек: тему, часовой пояс, счёт по умолчанию, версию и номер схемы.
/// </summary>
public sealed class SettingsSummaryQuery : ISettingsSummaryQuery
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly ILocalSettings _settings;
    private readonly IClock _clock;
    private readonly AboutInfo _about;

    /// <summary>
    /// Создаёт запрос.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    /// <param name="settings">Локальные настройки устройства.</param>
    /// <param name="clock">Часы приложения: у них действующий пояс.</param>
    /// <param name="about">Версия приложения из манифеста.</param>
    public SettingsSummaryQuery(
        IDbContextFactory<FinanceDbContext> contexts,
        ILocalSettings settings,
        IClock clock,
        AboutInfo about)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(about);

        _contexts = contexts;
        _settings = settings;
        _clock = clock;
        _about = about;
    }

    /// <inheritdoc />
    public async Task<SettingsSummary> ReadAsync(CancellationToken cancellationToken = default)
    {
        string? theme = await _settings
            .GetAsync(SettingName.Theme, cancellationToken)
            .ConfigureAwait(false);

        string? zone = await _settings
            .GetAsync(SettingName.TimeZoneId, cancellationToken)
            .ConfigureAwait(false);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Номер схемы берётся у самой базы, а не у сборки: показать нужно то,
        // что накатано на этом устройстве, а не то, что умеет код
        IEnumerable<string> applied = await context.Database
            .GetAppliedMigrationsAsync(cancellationToken)
            .ConfigureAwait(false);

        (_, OpenAccount? byDefault) = await DefaultAccount.ResolveAsync(context, cancellationToken).ConfigureAwait(false);

        return new SettingsSummary
        {
            Theme = Theme.Parse(theme),
            // Пояс берётся у часов, а не из настройки: при незнакомом
            // идентификаторе запуск откатывается на системный, и подпись
            // обязана называть тот пояс, по которому приложение правда считает
            TimeZoneId = _clock.TimeZone.Id,
            TimeZoneFromSystem = zone is null,
            DefaultAccount = byDefault,
            Version = _about.Version,
            Schema = applied.Count()
        };
    }
}

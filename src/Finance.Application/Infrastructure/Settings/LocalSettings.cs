using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Настройки в таблице <c>settings</c> локальной базы.
/// </summary>
public sealed class LocalSettings : ILocalSettings
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;

    /// <summary>
    /// Создаёт службу настроек.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    public LocalSettings(IDbContextFactory<FinanceDbContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Settings
            .AsNoTracking()
            .Where(row => row.Name == name)
            .Select(row => row.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        SettingRow? row = await context.Settings
            .FirstOrDefaultAsync(existing => existing.Name == name, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            context.Settings.Add(new SettingRow { Name = name, Value = value });
        }
        else
        {
            row.Value = value;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        SettingRow? row = await context.Settings
            .FirstOrDefaultAsync(existing => existing.Name == name, cancellationToken)
            .ConfigureAwait(false);

        // Настройки нет — значит она уже в том состоянии, которого от неё хотят:
        // отсутствие записи и есть «как в системе»
        if (row is null)
        {
            return;
        }

        // Физическое удаление, а не мягкое: настройки живут только на этом
        // устройстве и обмену не подлежат — узнавать об их удалении некому
        context.Settings.Remove(row);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

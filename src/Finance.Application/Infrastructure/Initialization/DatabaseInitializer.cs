using Finance.Application.Infrastructure;
using System.Globalization;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Infrastructure.Initialization;

/// <summary>
/// Записывает стартовый набор категорий при первом запуске. Ровно один раз
/// за всю жизнь установки.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly ILocalSettings _settings;

    /// <summary>
    /// Создаёт инициализацию базы.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    /// <param name="settings">Локальные настройки: в них хранится номер применённой версии набора.</param>
    public DatabaseInitializer(IDbContextFactory<FinanceDbContext> contexts, ILocalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(settings);

        _contexts = contexts;
        _settings = settings;
    }

    /// <summary>
    /// Записывает набор, если он ещё не записывался. Повторно набор не применяется
    /// никогда: пользователь вправе переименовать, перенести и удалить что угодно
    /// из него, и второй прогон вернул бы удалённое и затёр переименования.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? applied = await _settings
            .GetAsync(SettingName.PresetVersion, cancellationToken)
            .ConfigureAwait(false);

        if (applied is not null)
        {
            return;
        }

        Preset preset = Preset.Embedded();

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Метка изменения у строк набора фиксированная и заведомо давняя:
        // проставь ей текущее время — и переустановка приложения выглядела бы
        // для будущего обмена свежее пользовательских переименований
        context.KeepGivenTimestamps = true;
        context.Categories.AddRange(BuildRows(preset));

        // Номер версии пишется тем же сохранением, что и сами категории.
        // Порознь они переживают падение между собой: набор уже записан, отметки нет,
        // и следующий запуск вставляет те же выведенные ключи повторно — а они
        // детерминированные, и база отказывает по уникальности ключа навсегда
        context.Settings.Add(new SettingRow
        {
            Name = SettingName.PresetVersion,
            Value = preset.PresetVersion.ToString(CultureInfo.InvariantCulture)
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Раскладывает набор в строки таблицы категорий. Через доменные фабрики набор
    /// не проходит намеренно: они выдали бы новые ключи и текущее время, а здесь
    /// обязаны остаться выведенные из текстового ключа идентификаторы и давняя метка.
    /// </summary>
    private static IEnumerable<CategoryRow> BuildRows(Preset preset)
    {
        DateTimeOffset seededAt = preset.SeededAtUtc;

        foreach (PresetGroup group in preset.Groups)
        {
            EnsureKeyDerived(preset.Namespace, group.Key, group.Id);

            yield return new CategoryRow
            {
                Key = group.Id,
                ParentKey = null,
                Kind = group.Kind,
                Name = group.Name,
                Icon = group.Icon,
                Role = group.Role,
                ExcludeFromReports = group.ExcludeFromReports,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt
            };

            foreach (PresetSubcategory subcategory in group.Subcategories)
            {
                EnsureKeyDerived(preset.Namespace, subcategory.Key, subcategory.Id);

                yield return new CategoryRow
                {
                    Key = subcategory.Id,
                    ParentKey = group.Id,

                    // Вид хранится только на группе: подкатегория наследует его,
                    // и своя копия вида разошлась бы с ней при переносе
                    Kind = null,
                    Name = subcategory.Name,
                    Icon = subcategory.Icon,
                    Role = subcategory.Role,
                    ExcludeFromReports = subcategory.ExcludeFromReports,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt
                };
            }
        }
    }

    /// <summary>
    /// Идентификатор в файле обязан выводиться из текстового ключа. Расхождение
    /// означает, что ключ правили, а идентификатор — нет: на втором устройстве
    /// набор разъехался бы и склеить его было бы уже нечем.
    /// </summary>
    private static void EnsureKeyDerived(Guid namespaceKey, string key, Guid id)
    {
        Guid derived = Keys.Derive(namespaceKey, key);

        if (derived != id)
        {
            throw new InvalidOperationException(Faults.PresetIdMismatch(key, id, derived));
        }
    }
}

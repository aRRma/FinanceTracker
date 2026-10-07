using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Перекладывание категории между строкой базы и доменным типом.
/// </summary>
internal static class CategoryMapping
{
    extension(CategoryRow row)
    {
        /// <summary>
        /// Собирает доменную категорию из строки.
        /// </summary>
        public Category ToDomain() =>
            Category.Restore(
                row.Key,
                row.ParentKey,
                row.Kind,
                row.AcceptsAnyKind,
                row.Name,
                row.Icon,
                row.Role,
                row.ExcludeFromReports,
                row.CreatedAtUtc,
                row.UpdatedAtUtc,
                row.DeletedAtUtc,
                row.SyncedAtUtc,
                row.ExternalId);
    }

    extension(Category category)
    {
        /// <summary>
        /// Заводит новую строку по категории.
        /// </summary>
        public CategoryRow ToRow() =>
            new()
            {
                Key = category.Key,
                CreatedAtUtc = category.CreatedAtUtc,
                UpdatedAtUtc = category.UpdatedAtUtc,
                DeletedAtUtc = category.DeletedAtUtc,
                SyncedAtUtc = category.SyncedAtUtc,
                ExternalId = category.ExternalId,
                ParentKey = category.ParentKey,
                Kind = category.Kind,
                AcceptsAnyKind = category.AcceptsAnyKind,
                Name = category.Name,
                Icon = category.Icon,
                Role = category.Role,
                ExcludeFromReports = category.ExcludeFromReports
            };

        /// <summary>
        /// Переносит изменения категории в отслеживаемую строку.
        /// </summary>
        public void CopyTo(CategoryRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.ParentKey = category.ParentKey;
            row.Name = category.Name;
            row.Icon = category.Icon;
            row.DeletedAtUtc = category.DeletedAtUtc;
            row.SyncedAtUtc = category.SyncedAtUtc;
            row.ExternalId = category.ExternalId;

            // Вид, универсальность, роль и признак «вне отчётов» не переносятся: в домене
            // они без сеттеров, и менять их некому — подкатегория наследует вид,
            // а остальное задаётся при создании
        }
    }
}

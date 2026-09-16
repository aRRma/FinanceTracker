using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Общая часть отображения всех сущностей: ключ, технические метки и отсечение
/// мягко удалённых. Написанная в каждой конфигурации отдельно, она разошлась бы
/// при первой же правке, а разойтись ей нельзя — на ней держится мягкое удаление.
/// </summary>
internal static class EntityRowConfiguration
{
    extension<TRow>(EntityTypeBuilder<TRow> builder) where TRow : EntityRow
    {
        /// <summary>
        /// Описывает колонки, общие для всех сущностей, и фильтр мягкого удаления.
        /// </summary>
        public void ConfigureEntityColumns()
        {
            builder.HasKey(row => row.Key);

            // Ключ приходит с клиента готовым: записи создаются офлайн,
            // и автоинкремент базы не с чем было бы согласовать
            builder.Property(row => row.Key).HasColumnName("key").ValueGeneratedNever();

            builder.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            builder.Property(row => row.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            builder.Property(row => row.DeletedAtUtc).HasColumnName("deleted_at_utc");
            builder.Property(row => row.SyncedAtUtc).HasColumnName("synced_at_utc");
            builder.Property(row => row.ExternalId).HasColumnName("external_id");

            // Удалённое не видно нигде, кроме считанных мест, которые снимают фильтр
            // сами: проверка «были ли по счёту операции когда-либо» обязана видеть
            // и удалённые, иначе валюта счёта разблокируется после удаления операции
            builder.HasQueryFilter(row => row.DeletedAtUtc == null);
        }
    }
}

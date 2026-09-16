using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Таблица категорий: оба уровня в одной таблице, уровень задаёт <c>parent_key</c>.
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<CategoryRow>
{
    public void Configure(EntityTypeBuilder<CategoryRow> builder)
    {
        builder.ToTable("categories");
        builder.Property(row => row.ParentKey).HasColumnName("parent_key");
        builder.Property(row => row.Kind).HasColumnName("kind");
        builder.Property(row => row.Name).HasColumnName("name").IsRequired();
        builder.Property(row => row.Icon).HasColumnName("icon").IsRequired();
        builder.Property(row => row.Role).HasColumnName("role").IsRequired();
        builder.Property(row => row.ExcludeFromReports).HasColumnName("exclude_from_reports").IsRequired();

        builder.ConfigureEntityColumns();

        // Список подкатегорий группы читается на каждом выборе категории
        builder.HasIndex(row => row.ParentKey).HasDatabaseName("ix_categories_parent_key");
    }
}

using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Таблица счетов.
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<AccountRow>
{
    public void Configure(EntityTypeBuilder<AccountRow> builder)
    {
        builder.ToTable("accounts");
        builder.Property(row => row.Name).HasColumnName("name").IsRequired();
        builder.Property(row => row.Type).HasColumnName("type").IsRequired();
        builder.Property(row => row.Color).HasColumnName("color").IsRequired();
        builder.Property(row => row.Icon).HasColumnName("icon");
        builder.Property(row => row.Currency).HasColumnName("currency").IsRequired();
        builder.Property(row => row.OpeningBalance).HasColumnName("opening_balance").IsRequired();
        builder.Property(row => row.OpenedOn).HasColumnName("opened_on").IsRequired();
        builder.Property(row => row.ExcludedFromTotals).HasColumnName("excluded_from_totals").IsRequired();
        builder.Property(row => row.IsClosed).HasColumnName("is_closed").IsRequired();
        builder.Property(row => row.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.ConfigureEntityColumns();

        builder.HasIndex(row => row.SortOrder).HasDatabaseName("ix_accounts_sort_order");
    }
}

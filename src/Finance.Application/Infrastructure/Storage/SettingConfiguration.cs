using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>Локальные настройки устройства. Полей обмена у них нет намеренно.</summary>
internal sealed class SettingConfiguration : IEntityTypeConfiguration<SettingRow>
{
    public void Configure(EntityTypeBuilder<SettingRow> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(row => row.Name);

        builder.Property(row => row.Name).HasColumnName("name").ValueGeneratedNever();
        builder.Property(row => row.Value).HasColumnName("value").IsRequired();
    }
}

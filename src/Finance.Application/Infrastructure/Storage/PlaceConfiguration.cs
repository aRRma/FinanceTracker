using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>Справочник мест.</summary>
internal sealed class PlaceConfiguration : IEntityTypeConfiguration<PlaceRow>
{
    public void Configure(EntityTypeBuilder<PlaceRow> builder)
    {
        builder.ToTable("places");
        builder.Property(row => row.Name).HasColumnName("name").IsRequired();

        builder.ConfigureEntityColumns();

        // Справочник читается по алфавиту и отбирается по набранным буквам
        builder.HasIndex(row => row.Name).HasDatabaseName("ix_places_name");
    }
}

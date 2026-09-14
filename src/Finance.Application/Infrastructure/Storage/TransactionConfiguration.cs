using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Таблица операций и индексы под ленту. Индексов по счёту два — по счёту списания
/// и по счёту зачисления, — потому что лента счёта собирается объединением двух
/// выборок: условие <c>source = ? OR target = ?</c> индекс не берёт и при пятидесяти
/// тысячах операций превращается в полный проход. Третий, без ключа счёта, нужен
/// общей ленте: у тех двух ведущая колонка — счёт, и сортировку по всем счетам
/// они не обслуживают.
/// </summary>
internal sealed class TransactionConfiguration : IEntityTypeConfiguration<TransactionRow>
{
    private const string NotDeleted = "deleted_at_utc IS NULL";

    public void Configure(EntityTypeBuilder<TransactionRow> builder)
    {
        builder.ToTable("transactions");
        builder.Property(row => row.Kind).HasColumnName("kind").IsRequired();
        builder.Property(row => row.SourceAccountKey).HasColumnName("source_account_key").IsRequired();
        builder.Property(row => row.TargetAccountKey).HasColumnName("target_account_key");
        builder.Property(row => row.Amount).HasColumnName("amount").IsRequired();
        builder.Property(row => row.TargetAmount).HasColumnName("target_amount");
        builder.Property(row => row.CategoryKey).HasColumnName("category_key");
        builder.Property(row => row.PlaceKey).HasColumnName("place_key");
        builder.Property(row => row.OccurredOn).HasColumnName("occurred_on").IsRequired();
        builder.Property(row => row.Note).HasColumnName("note").HasMaxLength(Transaction.MaxNoteLength);

        builder.ConfigureEntityColumns();

        // Состав и порядок колонок повторяют сортировку ленты: дата, затем момент
        // создания, затем ключ как последний разрыватель ничьей
        builder.HasIndex(row => new { row.SourceAccountKey, row.OccurredOn, row.CreatedAtUtc, row.Key })
            .IsDescending(false, true, true, true)
            .HasFilter(NotDeleted)
            .HasDatabaseName("ix_transactions_source_feed");

        builder.HasIndex(row => new { row.TargetAccountKey, row.OccurredOn, row.CreatedAtUtc, row.Key })
            .IsDescending(false, true, true, true)
            .HasFilter(NotDeleted)
            .HasDatabaseName("ix_transactions_target_feed");

        // Под общую ленту: у индексов выше ведущая колонка — ключ счёта, и сортировку
        // ленты по всем счетам они не обслуживают. Без него главный список операций
        // идёт полным проходом с сортировкой во временном дереве. Он же берётся
        // на итоги дней общей ленты — там та же группировка по дате операции
        builder.HasIndex(row => new { row.OccurredOn, row.CreatedAtUtc, row.Key })
            .IsDescending(true, true, true)
            .HasFilter(NotDeleted)
            .HasDatabaseName("ix_transactions_feed");

        // Под отчёт: суммы за месяц по подкатегории
        builder.HasIndex(row => new { row.CategoryKey, row.OccurredOn })
            .HasFilter(NotDeleted)
            .HasDatabaseName("ix_transactions_category_occurred_on");

        // Под счётчики справочника мест
        builder.HasIndex(row => row.PlaceKey)
            .HasFilter(NotDeleted)
            .HasDatabaseName("ix_transactions_place_key");
    }
}

using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Контекст локальной базы. Создаётся фабрикой на каждую операцию: держать один
/// контекст на всё приложение значило бы копить в нём отслеживаемые сущности
/// до конца сеанса.
/// </summary>
public sealed class FinanceDbContext : DbContext
{
    private readonly IClock _clock;

    /// <summary>Создаёт контекст.</summary>
    /// <param name="options">Настройки подключения.</param>
    /// <param name="clock">Часы для метки изменения.</param>
    public FinanceDbContext(DbContextOptions<FinanceDbContext> options, IClock clock)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
    }

    /// <summary>
    /// Не трогать <c>updated_at_utc</c> при сохранении. Нужно ровно одному вызывающему —
    /// инициализации базы: у стартового набора метка фиксированная и заведомо давняя,
    /// иначе переустановка приложения затрёт пользовательские переименования.
    /// </summary>
    internal bool KeepGivenTimestamps { get; set; }

    internal DbSet<AccountRow> Accounts => Set<AccountRow>();

    internal DbSet<CategoryRow> Categories => Set<CategoryRow>();

    internal DbSet<PlaceRow> Places => Set<PlaceRow>();

    internal DbSet<TransactionRow> Transactions => Set<TransactionRow>();

    internal DbSet<SettingRow> Settings => Set<SettingRow>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Guid>().HaveConversion<GuidToTextConverter>();
        configurationBuilder.Properties<decimal>().HaveConversion<MinorUnitsConverter>();
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcMomentConverter>();

        // Перечисления хранятся именем, а не номером: дамп базы читается глазами,
        // а перестановка членов перечисления не переименовывает молча все записи
        configurationBuilder.Properties<AccountType>().HaveConversion<string>();
        configurationBuilder.Properties<Currency>().HaveConversion<string>().HaveMaxLength(3);
        configurationBuilder.Properties<CategoryKind>().HaveConversion<string>();
        configurationBuilder.Properties<CategoryRole>().HaveConversion<string>();
        configurationBuilder.Properties<TransactionKind>().HaveConversion<string>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Поимённо, а не сборкой целиком: строки внутренние, и поиск по сборке
        // нашёл бы их только отражением, которое режет компоновщик Android
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new PlaceConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new SettingConfiguration());
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampUpdates();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampUpdates();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Единая точка простановки <c>updated_at_utc</c>. В обработчиках её быть не должно:
    /// забытая в одном из них метка обнаружится только при обмене, когда правка
    /// не уедет на другое устройство.
    /// </summary>
    private void StampUpdates()
    {
        if (KeepGivenTimestamps)
        {
            return;
        }

        DateTimeOffset now = _clock.NowUtc;

        foreach (EntityEntry<EntityRow> entry in ChangeTracker.Entries<EntityRow>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        // Здесь же будет наполняться очередь отправки, когда появится обмен:
        // ключи изменённых строк известны ровно в этот момент и больше нигде
    }
}

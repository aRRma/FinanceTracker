using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Контекст для команд <c>dotnet ef</c>. Нужен потому, что обычный путь через
/// приложение здесь не работает: приложение собирается только под Android,
/// а миграции создаются на машине разработчика.
/// </summary>
/// <remarks>
/// Файл базы при этом не открывается — команде хватает описания модели.
/// </remarks>
public sealed class DesignTimeFinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    /// <inheritdoc />
    public FinanceDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<FinanceDbContext> options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new FinanceDbContext(options, new SystemClock(TimeProvider.System));
    }
}

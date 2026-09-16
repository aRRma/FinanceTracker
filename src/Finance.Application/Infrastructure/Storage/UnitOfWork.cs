using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Граница транзакции для команд. Каждая команда выполняется целиком или не
/// выполняется вовсе — иначе удаление подкатегории оставило бы её операции
/// висеть на удалённой категории.
/// </summary>
/// <remarks>
/// Вложенных транзакций в SQLite нет, поэтому вложенности нет и здесь: команда
/// получает свой контекст и свою транзакцию, а обработчики друг друга не вызывают.
/// </remarks>
public sealed class UnitOfWork
{
    private readonly IDbContextFactory<FinanceDbContext> _contexts;
    private readonly IChangeNotifier _notifier;

    /// <summary>
    /// Создаёт границу транзакции.
    /// </summary>
    /// <param name="contexts">Фабрика контекстов базы.</param>
    /// <param name="notifier">Оповещение экранов об изменении данных.</param>
    public UnitOfWork(IDbContextFactory<FinanceDbContext> contexts, IChangeNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(notifier);

        _contexts = contexts;
        _notifier = notifier;
    }

    /// <summary>
    /// Выполняет команду в транзакции и, если она зафиксировалась, оповещает экраны.
    /// </summary>
    /// <typeparam name="TResult">Что команда возвращает вызывающему.</typeparam>
    /// <param name="command">Тело команды. Возвращает результат и вид изменения.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<FinanceDbContext, CancellationToken, Task<(TResult Result, DataChange Change)>> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using FinanceDbContext context = await _contexts
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await using IDbContextTransaction transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        (TResult result, DataChange change) = await command(context, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // После фиксации, а не до: подписчик читает базу заново и обязан
        // увидеть уже зафиксированное состояние
        _notifier.Publish(change);

        return result;
    }

    /// <summary>
    /// Выполняет команду, которой нечего возвращать.
    /// </summary>
    /// <param name="command">Тело команды. Возвращает вид изменения.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public Task ExecuteAsync(
        Func<FinanceDbContext, CancellationToken, Task<DataChange>> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ExecuteAsync<object?>(
            async (context, token) => (null, await command(context, token).ConfigureAwait(false)),
            cancellationToken);
    }
}

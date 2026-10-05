namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Хранилище следа ПИН-кода на телефоне, зашифрованное ключом Android.
/// </summary>
/// <remarks>
/// В базе след уехал бы с выгрузкой в мессенджер, а четырёхзначный код по следу
/// подбирается за секунды.
/// </remarks>
public interface IPinStore
{
    /// <summary>
    /// Читает след; не задан или не читается — <c>null</c>.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает след, перекрывая прежний.
    /// </summary>
    /// <param name="trace">След ПИН-кода.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task WriteAsync(string trace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Стирает след.
    /// </summary>
    void Remove();
}

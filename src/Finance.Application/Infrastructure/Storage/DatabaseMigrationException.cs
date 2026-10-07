namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Подготовка базы не удалась: не создалась на первой установке, не снялась резервная
/// копия или сорвалась миграция, и тогда база возвращена из копии. Приложение после
/// этого не запускается: новый код со старой схемой работать не может, а пользователь
/// возвращает предыдущую версию приложения — данные при этом целы.
/// </summary>
/// <param name="message">Что именно случилось.</param>
/// <param name="innerException">Исходная ошибка миграции.</param>
public sealed class DatabaseMigrationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

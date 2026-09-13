namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Миграция схемы не удалась, база возвращена из резервной копии. Приложение после
/// этого не запускается: новый код со старой схемой работать не может, а пользователь
/// возвращает предыдущую версию приложения — данные при этом целы.
/// </summary>
/// <param name="message">Что именно случилось.</param>
/// <param name="innerException">Исходная ошибка миграции.</param>
public sealed class DatabaseMigrationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

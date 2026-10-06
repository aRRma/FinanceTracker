namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Причина завершения процесса, как её называет Android. Номера совпадают с системными
/// (<c>ApplicationExitInfo.REASON_*</c>): по ним запись системы и переводится в член.
/// </summary>
public enum ExitReason
{
    /// <summary>
    /// Причина неизвестна.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Процесс завершился сам; с кодом не 0 — необработанное исключение .NET вне главного потока.
    /// </summary>
    ExitSelf = 1,

    /// <summary>
    /// Процесс убит сигналом.
    /// </summary>
    Signaled = 2,

    /// <summary>
    /// Убит системой при нехватке памяти.
    /// </summary>
    LowMemory = 3,

    /// <summary>
    /// Необработанное исключение Java.
    /// </summary>
    Crash = 4,

    /// <summary>
    /// Нативное падение: к записи приложена трасса.
    /// </summary>
    CrashNative = 5,

    /// <summary>
    /// Зависание, после которого приложение закрыли: к записи приложена трасса.
    /// </summary>
    Anr = 6,

    /// <summary>
    /// Процесс не смог подняться.
    /// </summary>
    InitializationFailure = 7,

    /// <summary>
    /// Отозвано разрешение.
    /// </summary>
    Permission = 8,

    /// <summary>
    /// Убит за избыточный расход ресурсов — например, обмен с системой в фоне.
    /// </summary>
    ExcessiveResourceUsage = 9,

    /// <summary>
    /// Закрыт пользователем или по запросу: смахнут из недавних, остановлен принудительно.
    /// </summary>
    UserRequested = 10,

    /// <summary>
    /// Пользователь остановил приложение.
    /// </summary>
    UserStopped = 11,

    /// <summary>
    /// Умер процесс, от которого этот зависел.
    /// </summary>
    DependencyDied = 12,

    /// <summary>
    /// Прочая причина, названная системой в описании.
    /// </summary>
    Other = 13,

    /// <summary>
    /// Убит в замороженном состоянии.
    /// </summary>
    Freezer = 14,

    /// <summary>
    /// Пакет выключен или сменил состояние.
    /// </summary>
    PackageStateChange = 15,

    /// <summary>
    /// Приложение обновлено.
    /// </summary>
    PackageUpdated = 16,
}

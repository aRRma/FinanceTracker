namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Предложение защитить вход — один раз, при первом запуске после установки.
/// </summary>
/// <remarks>
/// Установку узнаём по базе, созданной этим запуском, а не по отсутствию отметки
/// «уже предлагали»: отметки нет и у тех, кто обновил стоящее приложение, и им
/// предложение всплыло бы поверх привычной работы. Восстановление из выгрузки
/// базу не создаёт — перезапуск после него предложения не повторяет.
/// </remarks>
public sealed class AppLockOffer
{
    private readonly FinanceStartup _startup;
    private readonly AppLockService _appLock;

    private bool _taken;

    /// <summary>
    /// Создаёт предложение.
    /// </summary>
    /// <param name="startup">Подготовка приложения: создана ли база этим запуском.</param>
    /// <param name="appLock">Защита входа.</param>
    public AppLockOffer(FinanceStartup startup, AppLockService appLock)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(appLock);

        _startup = startup;
        _appLock = appLock;
    }

    /// <summary>
    /// Предлагать ли сейчас. Отвечает «да» не больше одного раза за запуск.
    /// </summary>
    public bool Take()
    {
        if (_taken || !_startup.CreatedDatabase || _appLock.IsEnabled)
        {
            return false;
        }

        _taken = true;

        return true;
    }
}

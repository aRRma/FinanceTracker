namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Числа, которые живут на телефоне вне базы: признак защиты входа, счёт неверных
/// попыток и пауза.
/// </summary>
/// <remarks>
/// Не <c>ILocalSettings</c>: таблица настроек уезжает с выгрузкой, а защита входа
/// с ней переезжать не должна. Чтение синхронное — заслонка решает, подниматься ли,
/// до первого кадра.
/// </remarks>
public interface IDevicePreferences
{
    /// <summary>
    /// Читает число; незаданное — <c>null</c>.
    /// </summary>
    /// <param name="name">Имя записи.</param>
    long? Get(string name);

    /// <summary>
    /// Записывает число, перекрывая прежнее.
    /// </summary>
    /// <param name="name">Имя записи.</param>
    /// <param name="value">Значение.</param>
    void Set(string name, long value);

    /// <summary>
    /// Убирает запись.
    /// </summary>
    /// <param name="name">Имя записи.</param>
    void Remove(string name);
}

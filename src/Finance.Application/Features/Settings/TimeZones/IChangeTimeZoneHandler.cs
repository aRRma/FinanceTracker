namespace Finance.Application.Features.Settings.TimeZones;

/// <summary>
/// Выбор часового пояса. Модель представления зовёт его напрямую.
/// </summary>
public interface IChangeTimeZoneHandler
{
    /// <summary>
    /// Ставит пояс приложению и запоминает выбор. Пустой идентификатор — возврат
    /// к системному поясу: он стирает настройку, а не записывает слово «системный».
    /// </summary>
    /// <param name="id">Идентификатор зоны или <c>null</c> для системного пояса.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="TimeZoneNotFoundException">Системе такая зона неизвестна.</exception>
    Task HandleAsync(string? id, CancellationToken cancellationToken = default);
}

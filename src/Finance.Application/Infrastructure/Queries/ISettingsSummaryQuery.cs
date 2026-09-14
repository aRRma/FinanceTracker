namespace Finance.Application.Infrastructure.Queries;

/// <summary>
/// Состояние настроек для подписей и экрана «О программе». Общий запрос, а не
/// часть слайса настроек: раздел «Ещё» подписывает им свои строки, а слайсы
/// друг на друга не ссылаются.
/// </summary>
public interface ISettingsSummaryQuery
{
    /// <summary>Читает текущее состояние настроек.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<SettingsSummary> ReadAsync(CancellationToken cancellationToken = default);
}

using Finance.Domain.Enums;

namespace Finance.Application.Features.Transactions.Pick;

/// <summary>
/// Самые частые подкатегории для панели над списком выбора. Модель представления
/// зовёт запрос напрямую.
/// </summary>
public interface IFrequentCategoriesQuery
{
    /// <summary>
    /// Читает подкатегории заданного вида, на которые пришлось больше всего
    /// операций за последние месяцы, от частых к редким.
    /// </summary>
    /// <param name="kind">Вид: расход или доход — он задан видом операции.</param>
    /// <param name="limit">Сколько подкатегорий вернуть.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<IReadOnlyList<FrequentCategory>> ReadAsync(
        CategoryKind kind,
        int limit,
        CancellationToken cancellationToken = default);
}

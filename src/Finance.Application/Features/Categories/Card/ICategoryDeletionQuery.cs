namespace Finance.Application.Features.Categories.Card;

/// <summary>Что сказать в диалоге перед удалением подкатегории.</summary>
public interface ICategoryDeletionQuery
{
    /// <summary>
    /// Читает последствия удаления. Пусто, если удалять нечего или нельзя:
    /// категории нет, это группа, приёмник или служебная — у таких и кнопки удаления
    /// на экране не бывает.
    /// </summary>
    /// <param name="key">Ключ подкатегории.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task<CategoryDeletion?> ReadAsync(Guid key, CancellationToken cancellationToken = default);
}

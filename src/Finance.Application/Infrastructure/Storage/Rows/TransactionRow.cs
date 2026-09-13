using Finance.Domain;

namespace Finance.Application.Infrastructure.Storage.Rows;

/// <summary>
/// Строка таблицы операций. Валюты у сумм нет: она берётся у счёта своей стороны —
/// у <see cref="SourceAccountKey"/> для <see cref="Amount"/>, у <see cref="TargetAccountKey"/>
/// для <see cref="TargetAmount"/>.
/// </summary>
internal sealed class TransactionRow : EntityRow
{
    /// <summary>Доход, расход или перевод.</summary>
    public required TransactionKind Kind { get; set; }

    /// <summary>Счёт списания. У дохода и расхода — единственный счёт операции.</summary>
    public required Guid SourceAccountKey { get; set; }

    /// <summary>Счёт зачисления. Только у перевода.</summary>
    public Guid? TargetAccountKey { get; set; }

    /// <summary>Сумма в валюте счёта списания. Всегда положительна.</summary>
    public required decimal Amount { get; set; }

    /// <summary>Сумма зачисления в валюте счёта зачисления. Есть у любого перевода.</summary>
    public decimal? TargetAmount { get; set; }

    /// <summary>Подкатегория. У перевода пусто.</summary>
    public Guid? CategoryKey { get; set; }

    /// <summary>Место. Необязательно, у перевода запрещено.</summary>
    public Guid? PlaceKey { get; set; }

    /// <summary>Календарная дата операции.</summary>
    public required DateOnly OccurredOn { get; set; }

    /// <summary>Заметка. Пустая не хранится.</summary>
    public string? Note { get; set; }
}

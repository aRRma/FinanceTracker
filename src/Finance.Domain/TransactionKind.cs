namespace Finance.Domain;

/// <summary>Вид операции. Определяет знак суммы в балансе; сама сумма всегда положительна.</summary>
public enum TransactionKind
{
    /// <summary>Расход: уменьшает баланс счёта списания.</summary>
    Expense,

    /// <summary>Доход: увеличивает баланс счёта списания.</summary>
    Income,

    /// <summary>Перевод между двумя своими счетами. Не доход и не расход: в отчёт не входит.</summary>
    Transfer
}

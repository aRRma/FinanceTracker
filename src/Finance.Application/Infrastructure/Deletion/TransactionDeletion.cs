using Finance.Application.Texts;

namespace Finance.Application.Infrastructure.Deletion;

/// <summary>
/// Последствия удаления операций и текст диалога о них. Текст собирается здесь,
/// а не на каждом экране: форма операции и лента обязаны говорить одинаково.
/// </summary>
public sealed record TransactionDeletion
{
    /// <summary>
    /// Сколько операций будет удалено.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Балансы счетов после удаления — в порядке, в каком счета встречаются в ленте
    /// сверху вниз; у перевода сначала счёт списания, потом зачисления.
    /// </summary>
    public required IReadOnlyList<BalanceAfterDeletion> Balances { get; init; }

    /// <summary>
    /// Заголовок-вопрос: «Удалить операцию?» или «Удалить 5 операций?». Ни одной —
    /// операцию уже удалили, пока форма была открыта, и спрашивают о той, что на виду;
    /// лента в этом случае не спрашивает вовсе.
    /// </summary>
    public string Title => Count <= 1
        ? UiTexts.TransactionDeleteConfirmTitle
        : string.Format(
            UiCulture.Current,
            UiTexts.TransactionDeleteManyConfirmTitle,
            Plural.Of(Count, UiTexts.TransactionsCountOne, UiTexts.TransactionsCountFew, UiTexts.TransactionsCountMany));

    /// <summary>
    /// Текст диалога: каким станет баланс каждого счёта и что отменить будет нельзя.
    /// Без последствия пользователь подтверждает вслепую и проверяет результат потом.
    /// </summary>
    public string Message
    {
        get
        {
            List<string> sentences = [];

            foreach (BalanceAfterDeletion balance in Balances)
            {
                sentences.Add(string.Format(
                    UiCulture.Current,
                    UiTexts.TransactionBalanceAfter,
                    balance.AccountName,
                    balance.Balance.Display));
            }

            sentences.Add(UiTexts.TransactionDeleteIrreversible);

            return string.Join(' ', sentences);
        }
    }
}

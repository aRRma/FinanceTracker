using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Перекладывание операции между строкой базы и доменным типом. Валюты в строке нет:
/// она приходит от счетов операции, которые на пишущем пути всё равно прочитаны —
/// без них не выполнить проверки <see cref="TransactionRules"/>.
/// </summary>
internal static class TransactionMapping
{
    extension(TransactionRow row)
    {
        /// <summary>Собирает доменную операцию из строки.</summary>
        /// <param name="sourceCurrency">Валюта счёта списания — валюта суммы операции.</param>
        /// <param name="targetCurrency">Валюта счёта зачисления — валюта суммы зачисления, у перевода.</param>
        public Transaction ToDomain(Currency sourceCurrency, Currency? targetCurrency)
        {
            Money? targetAmount = (row.TargetAmount, targetCurrency) switch
            {
                (decimal amount, Currency currency) => Money.Restore(amount, currency),
                (null, _) => null,

                // Сумма зачисления без валюты второго счёта не собирается,
                // и молча обнулить её нельзя: перевод потерял бы вторую сторону
                _ => throw new ArgumentNullException(
                    nameof(targetCurrency),
                    $"У операции {row.Key} есть сумма зачисления, а валюта счёта зачисления не передана")
            };

            return Transaction.Restore(
                row.Key,
                row.Kind,
                row.SourceAccountKey,
                row.TargetAccountKey,
                Money.Restore(row.Amount, sourceCurrency),
                targetAmount,
                row.CategoryKey,
                row.PlaceKey,
                row.OccurredOn,
                row.Note,
                row.CreatedAtUtc,
                row.UpdatedAtUtc,
                row.DeletedAtUtc,
                row.SyncedAtUtc,
                row.ExternalId);
        }
    }

    extension(Transaction transaction)
    {
        /// <summary>Заводит новую строку по операции.</summary>
        public TransactionRow ToRow() =>
            new()
            {
                Key = transaction.Key,
                CreatedAtUtc = transaction.CreatedAtUtc,
                UpdatedAtUtc = transaction.UpdatedAtUtc,
                DeletedAtUtc = transaction.DeletedAtUtc,
                SyncedAtUtc = transaction.SyncedAtUtc,
                ExternalId = transaction.ExternalId,
                Kind = transaction.Kind,
                SourceAccountKey = transaction.SourceAccountKey,
                TargetAccountKey = transaction.TargetAccountKey,
                Amount = transaction.Amount.Amount,
                TargetAmount = transaction.TargetAmount?.Amount,
                CategoryKey = transaction.CategoryKey,
                PlaceKey = transaction.PlaceKey,
                OccurredOn = transaction.OccurredOn,
                Note = transaction.Note
            };

        /// <summary>Переносит изменения операции в отслеживаемую строку.</summary>
        public void CopyTo(TransactionRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.Kind = transaction.Kind;
            row.SourceAccountKey = transaction.SourceAccountKey;
            row.TargetAccountKey = transaction.TargetAccountKey;
            row.Amount = transaction.Amount.Amount;
            row.TargetAmount = transaction.TargetAmount?.Amount;
            row.CategoryKey = transaction.CategoryKey;
            row.PlaceKey = transaction.PlaceKey;
            row.OccurredOn = transaction.OccurredOn;
            row.Note = transaction.Note;
            row.DeletedAtUtc = transaction.DeletedAtUtc;
            row.SyncedAtUtc = transaction.SyncedAtUtc;
            row.ExternalId = transaction.ExternalId;
        }
    }
}

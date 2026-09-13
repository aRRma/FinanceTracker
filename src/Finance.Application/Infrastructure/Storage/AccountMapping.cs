using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Перекладывание счёта между строкой базы и доменным типом. Чтение идёт через
/// <see cref="Account.Restore"/>: строка уже прошла проверку при вводе, и повторная
/// превратила бы чтение списка счетов в валидацию, способную упасть.
/// </summary>
internal static class AccountMapping
{
    extension(AccountRow row)
    {
        /// <summary>Собирает доменный счёт из строки.</summary>
        public Account ToDomain() =>
            Account.Restore(
                row.Key,
                row.Name,
                row.Type,
                row.Currency,
                row.OpeningBalance,
                row.OpenedOn,
                row.ExcludedFromTotals,
                row.IsClosed,
                row.SortOrder,
                row.CreatedAtUtc,
                row.UpdatedAtUtc,
                row.DeletedAtUtc,
                row.SyncedAtUtc,
                row.ExternalId);
    }

    extension(Account account)
    {
        /// <summary>Заводит новую строку по счёту.</summary>
        public AccountRow ToRow() =>
            new()
            {
                Key = account.Key,
                CreatedAtUtc = account.CreatedAtUtc,
                UpdatedAtUtc = account.UpdatedAtUtc,
                DeletedAtUtc = account.DeletedAtUtc,
                SyncedAtUtc = account.SyncedAtUtc,
                ExternalId = account.ExternalId,
                Name = account.Name,
                Type = account.Type,
                Currency = account.Currency,
                OpeningBalance = account.OpeningBalance.Amount,
                OpenedOn = account.OpenedOn,
                ExcludedFromTotals = account.ExcludedFromTotals,
                IsClosed = account.IsClosed,
                SortOrder = account.SortOrder
            };

        /// <summary>Переносит изменения счёта в отслеживаемую строку.</summary>
        public void CopyTo(AccountRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.Name = account.Name;
            row.Type = account.Type;
            row.Currency = account.Currency;
            row.OpeningBalance = account.OpeningBalance.Amount;
            row.OpenedOn = account.OpenedOn;
            row.ExcludedFromTotals = account.ExcludedFromTotals;
            row.IsClosed = account.IsClosed;
            row.SortOrder = account.SortOrder;
            row.DeletedAtUtc = account.DeletedAtUtc;
            row.SyncedAtUtc = account.SyncedAtUtc;
            row.ExternalId = account.ExternalId;
        }
    }
}

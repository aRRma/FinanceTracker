using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

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
        /// <summary>
        /// Собирает доменный счёт из строки.
        /// </summary>
        public Account ToDomain() =>
            Account.Restore(
                row.Key,
                row.Name,
                row.Type,
                row.Color,
                row.Icon,
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
        /// <summary>
        /// Заводит новую строку по счёту.
        /// </summary>
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
                Color = account.Color,
                Icon = account.Icon,
                Currency = account.Currency,
                OpeningBalance = account.OpeningBalance.Amount,
                OpenedOn = account.OpenedOn,
                ExcludedFromTotals = account.ExcludedFromTotals,
                IsClosed = account.IsClosed,
                SortOrder = account.SortOrder
            };

        /// <summary>
        /// Переносит изменения счёта в отслеживаемую строку.
        /// </summary>
        public void CopyTo(AccountRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.Name = account.Name;
            row.Type = account.Type;
            row.Color = account.Color;
            row.Icon = account.Icon;
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

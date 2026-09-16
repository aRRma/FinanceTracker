using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;

namespace Finance.Application.Infrastructure.Storage;

/// <summary>
/// Перекладывание места между строкой базы и доменным типом.
/// </summary>
internal static class PlaceMapping
{
    extension(PlaceRow row)
    {
        /// <summary>
        /// Собирает доменное место из строки.
        /// </summary>
        public Place ToDomain() =>
            Place.Restore(
                row.Key,
                row.Name,
                row.CreatedAtUtc,
                row.UpdatedAtUtc,
                row.DeletedAtUtc,
                row.SyncedAtUtc,
                row.ExternalId);
    }

    extension(Place place)
    {
        /// <summary>
        /// Заводит новую строку по месту.
        /// </summary>
        public PlaceRow ToRow() =>
            new()
            {
                Key = place.Key,
                CreatedAtUtc = place.CreatedAtUtc,
                UpdatedAtUtc = place.UpdatedAtUtc,
                DeletedAtUtc = place.DeletedAtUtc,
                SyncedAtUtc = place.SyncedAtUtc,
                ExternalId = place.ExternalId,
                Name = place.Name
            };

        /// <summary>
        /// Переносит изменения места в отслеживаемую строку.
        /// </summary>
        public void CopyTo(PlaceRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.Name = place.Name;
            row.DeletedAtUtc = place.DeletedAtUtc;
            row.SyncedAtUtc = place.SyncedAtUtc;
            row.ExternalId = place.ExternalId;
        }
    }
}

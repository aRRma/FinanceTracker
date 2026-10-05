using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Settings;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Settings.DefaultAccounts;

/// <summary>
/// Меняет счёт по умолчанию. Пишется только ключ, и только незаблокированного счёта:
/// выбор, записанный на заблокированный по устаревшему списку, ожил бы при разблокировке,
/// хотя блокировка выбор забывает. Проверка и запись — одной транзакцией.
/// </summary>
public sealed class ChangeDefaultAccountHandler : IChangeDefaultAccountHandler
{
    private readonly UnitOfWork _unitOfWork;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="unitOfWork">Граница транзакции; она же оповещает экраны о смене настройки.</param>
    public ChangeDefaultAccountHandler(UnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public Task<bool> HandleAsync(Guid accountKey, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync<bool>(
            async (context, token) =>
            {
                // Удалённый под глобальный фильтр не попадает, заблокированный отсекается условием
                bool open = await context.Accounts
                    .AnyAsync(row => row.Key == accountKey && !row.IsClosed, token)
                    .ConfigureAwait(false);

                if (!open)
                {
                    return (false, DataChange.None);
                }

                string value = accountKey.ToString();

                SettingRow? row = await context.Settings
                    .FirstOrDefaultAsync(static existing => existing.Name == SettingName.DefaultAccountKey, token)
                    .ConfigureAwait(false);

                if (row is null)
                {
                    context.Settings.Add(new SettingRow { Name = SettingName.DefaultAccountKey, Value = value });
                }
                else
                {
                    row.Value = value;
                }

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                // Подпись строки в «Ещё» называет счёт по умолчанию и обязана смениться вместе с ним
                return (true, DataChange.Settings);
            },
            cancellationToken);
}

using Finance.Domain.Values;

namespace Finance.Application.Infrastructure.Deletion;

/// <summary>
/// Баланс счёта, каким он станет после удаления операций.
/// </summary>
/// <param name="AccountName">Название счёта.</param>
/// <param name="Balance">Баланс после удаления.</param>
public sealed record BalanceAfterDeletion(string AccountName, Money Balance);

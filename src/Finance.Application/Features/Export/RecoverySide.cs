namespace Finance.Application.Features.Export;

/// <summary>
/// Что лежит в рабочей базе: по этим числам пользователь видит, что удалит
/// восстановление, и понимает, нужно ли о нём предупреждать.
/// </summary>
public sealed record RecoverySide
{
    /// <summary>
    /// Сколько счетов, без удалённых.
    /// </summary>
    public required int Accounts { get; init; }

    /// <summary>
    /// Сколько операций, без удалённых.
    /// </summary>
    public required int Transactions { get; init; }

    /// <summary>
    /// Есть ли что терять: счёт или операция. Стартовые категории не в счёт —
    /// они есть в любой базе с первого запуска.
    /// </summary>
    public bool HasData => Accounts > 0 || Transactions > 0;
}

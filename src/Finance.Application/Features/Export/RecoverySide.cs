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
    /// Сколько категорий завёл пользователь — групп и подкатегорий, без удалённых.
    /// </summary>
    /// <remarks>
    /// Стартовые не в счёт: они есть в любой базе с первого запуска и вернутся с файлом.
    /// «Прочее», заведённое приложением вместе с группой, — тоже.
    /// </remarks>
    public required int Categories { get; init; }

    /// <summary>
    /// Сколько мест, без удалённых.
    /// </summary>
    public required int Places { get; init; }

    /// <summary>
    /// Есть ли что терять: хоть что-то, заведённое пользователем.
    /// </summary>
    public bool HasData => Accounts > 0 || Transactions > 0 || Categories > 0 || Places > 0;
}

using Finance.Domain.Enums;

namespace Finance.Application.Features.WalletImport;

/// <summary>
/// Операция в файле переноса. Ссылки на счета и места — именами из того же файла,
/// на подкатегорию — текстовым ключом стартового набора: из него выведен её
/// идентификатор, и ключ переживает переименование подкатегории.
/// </summary>
/// <param name="Kind">Вид операции.</param>
/// <param name="SourceAccount">Счёт списания; у дохода — счёт, куда пришли деньги.</param>
/// <param name="TargetAccount">Счёт зачисления — только у перевода.</param>
/// <param name="Amount">Сумма в валюте счёта; у перевода валюты сторон совпадают.</param>
/// <param name="Category">Ключ подкатегории стартового набора — у дохода и расхода.</param>
/// <param name="Place">Место — необязательно.</param>
/// <param name="OccurredOn">Дата операции, местная.</param>
/// <param name="CreatedAtUtc">Когда запись завели в Wallet: по нему лента упорядочивает операции одного дня.</param>
/// <param name="Note">Заметка — необязательно.</param>
public sealed record WalletImportTransaction(
    TransactionKind Kind,
    string SourceAccount,
    string? TargetAccount,
    decimal Amount,
    string? Category,
    string? Place,
    DateOnly OccurredOn,
    DateTimeOffset CreatedAtUtc,
    string? Note);

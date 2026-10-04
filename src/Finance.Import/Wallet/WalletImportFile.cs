using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain.Enums;

namespace Finance.Import.Wallet;

/// <summary>
/// Файл переноса истории из Wallet. Его готовит тулза задачи на рабочей машине
/// (она в архиве задачи вне репозитория): сопоставление чужих справочников
/// с нашими правится там текстом, а этот формат остаётся неизменным.
/// </summary>
/// <param name="Format">Метка формата: чужой JSON с похожими полями не должен разобраться молча.</param>
/// <param name="Version">Версия формата.</param>
/// <param name="Accounts">Счета в порядке главного экрана.</param>
/// <param name="Places">Места, на которые ссылаются операции.</param>
/// <param name="Transactions">Операции.</param>
public sealed partial record WalletImportFile(
    string Format,
    int Version,
    IReadOnlyList<WalletImportAccount> Accounts,
    IReadOnlyList<string> Places,
    IReadOnlyList<WalletImportTransaction> Transactions)
{
    /// <summary>
    /// Метка формата файла переноса.
    /// </summary>
    public const string FormatName = "finance.wallet-import";

    /// <summary>
    /// Версия формата, которую читает перенос.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Разбирает файл. Незнакомое поле, пропущенное обязательное, чужая метка или версия,
    /// пустой элемент списка, число или <c>Unknown</c> вместо имени перечисления,
    /// момент записи не в UTC и имя с пробелами по краям — отказ, а не значение
    /// по умолчанию, которое записалось бы в базу.
    /// </summary>
    /// <param name="json">Поток с содержимым файла.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="JsonException">Файл не разбирается.</exception>
    public static async Task<WalletImportFile> ReadAsync(Stream json, CancellationToken cancellationToken = default)
    {
        WalletImportFile file = await JsonSerializer
            .DeserializeAsync(json, WalletImportJson.Default.WalletImportFile, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new JsonException(ImportFaults.WalletImportFileEmpty());

        if (file.Format != FormatName || file.Version != CurrentVersion)
        {
            throw new JsonException(ImportFaults.WalletImportFormat(file.Format, file.Version));
        }

        file.Validate();

        return file;
    }

    /// <summary>
    /// Пишет файл. Нужно тулзе подготовки: формат задаёт один тип на обе стороны,
    /// и разойтись тулзе с переносом не в чем.
    /// </summary>
    /// <param name="target">Куда писать.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public Task WriteAsync(Stream target, CancellationToken cancellationToken = default) =>
        JsonSerializer.SerializeAsync(target, this, WalletImportJson.Default.WalletImportFile, cancellationToken);

    /// <summary>
    /// То, чего разбор сам не ловит. Пометка «не пусто» у свойства не распространяется
    /// на элементы списка, <c>Unknown</c> — законный член перечисления, который домен
    /// намеренно не проверяет, а момент без смещения разобрался бы по поясу устройства.
    /// Ссылки на счёт и место — по имени, и имя с пробелом по краям не нашлось бы:
    /// домен запишет его обрезанным.
    /// </summary>
    private void Validate()
    {
        for (int index = 0; index < Accounts.Count; index++)
        {
            WalletImportAccount? account = Accounts[index];
            Require(account is not null, nameof(Accounts), index);
            Require(Named(account!.Name), nameof(Accounts), index);
            Require(Known(account.Type) && Known(account.Currency), nameof(Accounts), index);
        }

        for (int index = 0; index < Places.Count; index++)
        {
            Require(Named(Places[index]), nameof(Places), index);
        }

        for (int index = 0; index < Transactions.Count; index++)
        {
            WalletImportTransaction? item = Transactions[index];
            Require(item is not null, nameof(Transactions), index);
            Require(Known(item!.Kind) && item.CreatedAtUtc.Offset == TimeSpan.Zero, nameof(Transactions), index);
            Require(Named(item.SourceAccount) && Optional(item.TargetAccount) && Optional(item.Place), nameof(Transactions), index);
        }
    }

    private static void Require(bool condition, string list, int index)
    {
        if (!condition)
        {
            throw new JsonException(ImportFaults.WalletImportEntry(list, index));
        }
    }

    private static bool Named(string? name) => name is { Length: > 0 } && name.Trim().Length == name.Length;

    private static bool Optional(string? name) => name is null || Named(name);

    private static bool Known<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) && !EqualityComparer<TEnum>.Default.Equals(value, default);

    // Разбор генератором, а не отражением: переживает обрезку кода в релизе.
    // Строгость та же, что у стартового набора, а перечисления — только именем:
    // стандартный разбор по имени принял бы и номер члена
    [JsonSerializable(typeof(WalletImportFile))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        Converters =
        [
            typeof(NamedOnly<AccountType>),
            typeof(NamedOnly<Currency>),
            typeof(NamedOnly<TransactionKind>),
        ])]
    private sealed partial class WalletImportJson : JsonSerializerContext;

    /// <summary>
    /// Перечисление строго по имени: номер члена — отказ.
    /// </summary>
    /// <typeparam name="TEnum">Перечисление.</typeparam>
    private sealed class NamedOnly<TEnum>() : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
        where TEnum : struct, Enum;
}

namespace Finance.Application.Features.Export;

/// <summary>
/// Строка предупреждения перед заменой: что и сколько удалит восстановление — «2 счёта».
/// </summary>
public sealed record RecoveryLine
{
    /// <summary>
    /// Ключ значка — тот же, что у раздела этих данных на экране «Ещё».
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    /// Число со словом в нужной форме: «6 операций».
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Над строкой разделитель: у первой его нет, иначе карточка начиналась бы чертой.
    /// </summary>
    public required bool HasDivider { get; init; }
}

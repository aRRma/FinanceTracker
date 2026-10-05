namespace Finance.Application.Infrastructure.Settings;

/// <summary>
/// Незаблокированный счёт — кандидат в счета по умолчанию: ключ и имя, которым его называют подтверждения и раздел «Ещё».
/// </summary>
/// <param name="Key">Ключ счёта.</param>
/// <param name="Name">Наименование счёта.</param>
public sealed record OpenAccount(Guid Key, string Name);

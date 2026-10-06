namespace Finance.Application.Features.Report;

/// <summary>
/// Выбор счетов отчёта — один на приложение. Экран выбора кладёт его сюда, три уровня
/// отчёта читают при каждом чтении: передавать набор параметром маршрута значило бы
/// писать список ключей в адрес.
/// </summary>
/// <remarks>
/// Живёт в памяти, пока жив процесс, и в базу не пишется: после перезапуска отчёт
/// снова по активным рублёвым счетам. Набор, оставшийся с прошлой недели, легко
/// принять за обычный отчёт — подсветку строки глаз перестаёт замечать.
/// </remarks>
public sealed class ReportChoice
{
    /// <summary>
    /// По каким счетам считается отчёт.
    /// </summary>
    public ReportAccounts Accounts
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = ReportAccounts.Default;
}

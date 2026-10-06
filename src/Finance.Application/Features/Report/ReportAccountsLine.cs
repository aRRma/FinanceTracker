using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.Application.Features.Report;

/// <summary>
/// Строка счетов отчёта: подпись набора и знаки его счетов. По знакам видно, какой
/// набор выбран, не открывая экран выбора.
/// </summary>
/// <param name="Title">Подпись: «Активные счета · ₽», имя единственного счёта или «5 счетов».</param>
/// <param name="IsCustom">Набор не обычный — строка подсвечивается, на уровнях глубже видны знаки.</param>
/// <param name="Tokens">Счета, чьи знаки видны, в порядке экрана выбора, не больше пяти.</param>
/// <param name="More">Сколько счетов не уместилось: «+3»; пусто, если уместились все.</param>
/// <remarks>
/// Равенство — по содержимому, а не по ссылке на список знаков: каждое чтение
/// собирает строку заново, и без этого знаки пересоздавались бы на каждую смену месяца.
/// </remarks>
public sealed record ReportAccountsLine(
    string Title,
    bool IsCustom,
    IReadOnlyList<ReportAccount> Tokens,
    string More)
{
    /// <summary>
    /// Больше знаков в строке не помещается рядом с подписью на узком экране.
    /// </summary>
    private const int MaxTokens = 5;

    /// <summary>
    /// Строка до первого чтения — пустая, а не «Активные счета»: иначе подпись
    /// мелькнула бы и сменилась на выбранную.
    /// </summary>
    public static ReportAccountsLine Empty { get; } = new(string.Empty, IsCustom: false, [], string.Empty);

    /// <summary>
    /// Часть счетов не уместилась — рядом со знаками стоит «+N».
    /// </summary>
    public bool HasMore => More.Length > 0;

    /// <summary>
    /// Читает счета для знаков набора. Обычному набору знаки не нужны — подпись
    /// глубже первого уровня их при нём не показывает, — и обращение к базе пропускается.
    /// </summary>
    /// <param name="query">Запрос счетов отчёта.</param>
    /// <param name="accounts">Выбор счетов отчёта.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    internal static Task<IReadOnlyList<ReportAccount>> ReadForTokensAsync(
        IReportAccountsQuery query,
        ReportAccounts accounts,
        CancellationToken cancellationToken) =>
        accounts.IsDefault
            ? Task.FromResult<IReadOnlyList<ReportAccount>>([])
            : query.ReadAsync(cancellationToken);

    /// <summary>
    /// Собирает строку по выбору и списку счетов. Счета берутся из списка, а не из
    /// ключей выбора: удалённый счёт ни в число, ни в знаки не попадает.
    /// </summary>
    /// <param name="accounts">Выбор счетов отчёта.</param>
    /// <param name="all">Все неудалённые счета в порядке показа.</param>
    public static ReportAccountsLine Of(ReportAccounts accounts, IReadOnlyList<ReportAccount> all)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(all);

        List<ReportAccount> included = [];

        foreach (ReportAccount account in all)
        {
            if (accounts.Includes(account))
            {
                included.Add(account);
            }
        }

        string title = accounts.IsAllActive
            ? string.Format(UiCulture.Current, UiTexts.ReportAccountsActive, accounts.Currency.Symbol)
            : included.Count is 1
                ? included[0].Name
                : Plural.Of(included.Count, UiTexts.ReportAccountsCountOne, UiTexts.ReportAccountsCountFew, UiTexts.ReportAccountsCountMany);

        // Не уместились — на месте последнего знака встаёт число: «+1» вместо
        // пятого знака не сэкономил бы места, поэтому знаков тогда четыре
        int shown = included.Count <= MaxTokens ? included.Count : MaxTokens - 1;

        string more = shown < included.Count
            ? string.Create(UiCulture.Current, $"+{included.Count - shown}")
            : string.Empty;

        return new ReportAccountsLine(title, !accounts.IsDefault, included[..shown], more);
    }

    /// <inheritdoc />
    public bool Equals(ReportAccountsLine? other) =>
        other is not null
        && Title == other.Title
        && IsCustom == other.IsCustom
        && More == other.More
        && Tokens.SequenceEqual(other.Tokens);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Title, IsCustom, More, Tokens.Count);
}

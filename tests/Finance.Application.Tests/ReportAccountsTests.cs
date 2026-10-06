using System.Text.RegularExpressions;
using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Report;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Счета отчёта: суммы по выбранному набору на трёх уровнях, валюта набора, строка
/// счетов со знаками и экран выбора с «Все активные».
/// </summary>
public sealed partial class ReportAccountsTests
{
    /// <summary>
    /// Скрытый счёт, добавленный к набору, входит в суммы на всех трёх уровнях —
    /// ради этого выбор и заведён.
    /// </summary>
    [Fact]
    public async Task Скрытый_счёт_из_набора_входит_на_всех_уровнях()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Вклад", 10_000m, excluded: true);

        await given.SaveAsync(given.Expense(card, 300m));
        Guid fromSavings = await given.SaveAsync(given.Expense(savings, 700m));

        given.Database.Resolve<ReportChoice>().Accounts = ReportAccounts.Chosen(Currency.RUB, [card, savings]);

        ReportViewModel report = await LoadedAsync(given);
        ReportTotal group = Assert.Single(await given.ReportAsync(accounts: ReportAccounts.Chosen(Currency.RUB, [card, savings])));

        Assert.Equal(Money.Restore(-1_000m, Currency.RUB).DisplaySigned, report.Total);

        ReportGroupViewModel second = given.Database.Resolve<ReportGroupViewModel>();
        await second.LoadAsync(group.Key, ReportMonth.Of(given.Today));

        Assert.Equal(Money.Restore(-1_000m, Currency.RUB).DisplaySigned, second.Total);

        ReportSubcategoryViewModel third = given.Database.Resolve<ReportSubcategoryViewModel>();
        await third.LoadAsync(given.ExpenseCategory, ReportMonth.Of(given.Today));

        Assert.Equal(2, third.Rows.Count);
        Assert.Contains(third.Rows, row => row.Key == fromSavings);
    }

    /// <summary>
    /// Набор долларовых счетов считается в долларах: рублёвые операции не входят,
    /// итоги и строки — со знаком доллара, без смешения валют в одной сумме.
    /// </summary>
    [Fact]
    public async Task Набор_в_долларах_считается_в_долларах()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid dollars = await given.AccountAsync("Доллары дома", 1_000m, Currency.USD, excluded: true);

        await given.SaveAsync(given.Expense(card, 300m));
        await given.SaveAsync(given.Expense(dollars, 45.5m));

        given.Database.Resolve<ReportChoice>().Accounts = ReportAccounts.Chosen(Currency.USD, [dollars]);

        ReportViewModel report = await LoadedAsync(given);

        Assert.Equal(Money.Restore(-45.5m, Currency.USD).DisplaySigned, report.Total);
        Assert.Equal(Money.Restore(-45.5m, Currency.USD).DisplaySigned, Assert.Single(report.Rows).Amount);

        ReportGroupViewModel second = given.Database.Resolve<ReportGroupViewModel>();
        await second.LoadAsync(report.Rows[0].Key, ReportMonth.Of(given.Today));

        Assert.Equal(Money.Restore(-45.5m, Currency.USD).DisplaySigned, second.Total);
        Assert.Equal(Money.Restore(-45.5m, Currency.USD).DisplaySigned, Assert.Single(second.Rows).Amount);

        ReportSubcategoryViewModel third = given.Database.Resolve<ReportSubcategoryViewModel>();
        await third.LoadAsync(given.ExpenseCategory, ReportMonth.Of(given.Today));

        Assert.Equal(Money.Restore(-45.5m, Currency.USD).DisplaySigned, third.Total);
        Assert.Equal(Money.Restore(-45.5m, Currency.USD).DisplaySigned, Assert.Single(third.Rows).Amount);
    }

    /// <summary>
    /// «Все активные» другой валюты — правило, а не снимок: счёт, заведённый после
    /// выбора, входит сам, а накопления той же валюты — нет.
    /// </summary>
    [Fact]
    public async Task Все_активные_валюты_подхватывают_новый_счёт()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);
        Guid euroSavings = await given.AccountAsync("Вклад в евро", 1_000m, Currency.EUR, excluded: true);
        ReportAccounts accounts = ReportAccounts.AllActive(Currency.EUR);

        await given.SaveAsync(given.Expense(euro, 10m));
        await given.SaveAsync(given.Expense(euroSavings, 20m));

        Guid later = await given.AccountAsync("Вторая карта евро", 1_000m, Currency.EUR);
        await given.SaveAsync(given.Expense(later, 5m));

        ReportTotal group = Assert.Single(await given.ReportAsync(accounts: accounts));

        Assert.Equal(Money.Restore(15m, Currency.EUR), group.Total);
    }

    /// <summary>
    /// Счёт набора в другой валюте, чем у набора, выпадает из сумм: смешать валюты
    /// в одной сумме нельзя, даже если ключ остался в наборе.
    /// </summary>
    [Fact]
    public async Task Счёт_чужой_валюты_из_набора_выпадает()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid dollars = await given.AccountAsync("Доллары", 1_000m, Currency.USD);

        await given.SaveAsync(given.Expense(card, 300m));
        await given.SaveAsync(given.Expense(dollars, 45m));

        ReportTotal group = Assert.Single(await given.ReportAsync(accounts: ReportAccounts.Chosen(Currency.RUB, [card, dollars])));

        Assert.Equal(Money.Restore(300m, Currency.RUB), group.Total);
    }

    /// <summary>
    /// Совет сменить месяц считается по набору: операции по счетам вне него есть —
    /// совета нет; вне набора тоже пусто — совет есть.
    /// </summary>
    [Fact]
    public async Task Совет_сменить_месяц_считается_по_набору()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Вклад", 10_000m, excluded: true);

        given.Database.Resolve<ReportChoice>().Accounts = ReportAccounts.Chosen(Currency.RUB, [savings]);

        ReportViewModel quiet = await LoadedAsync(given);

        Assert.True(quiet.IsEmpty);
        Assert.True(quiet.HasEmptyHint);

        await given.SaveAsync(given.Expense(card, 300m));

        ReportViewModel busy = await LoadedAsync(given);

        Assert.True(busy.IsEmpty, "Расход по карте попал в отчёт по одному вкладу.");
        Assert.False(busy.HasEmptyHint, "Операции месяца есть, а пустой отчёт советует сменить месяц.");
    }

    /// <summary>
    /// Умолчание — тихая строка «Активные счета · ₽» со знаками активных рублёвых
    /// счетов, заблокированный — вполсилы; накопления и чужие валюты знаков не дают.
    /// </summary>
    [Fact]
    public async Task Строка_умолчания_называет_активные_рубли()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 10_000m);
        Guid old = await given.AccountAsync("Старая");
        await given.AccountAsync("Вклад", 10_000m, excluded: true);
        await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);
        await given.CloseAsync(old);

        ReportViewModel report = await LoadedAsync(given);
        ReportAccountsLine line = report.Accounts;

        Assert.Equal(string.Format(UiCulture.Current, UiTexts.ReportAccountsActive, Currency.RUB.Symbol), line.Title);
        Assert.False(line.IsCustom);
        Assert.Equal([false, true], line.Tokens.Select(static token => token.IsClosed));
        Assert.False(line.HasMore);
    }

    /// <summary>
    /// Подпись своего набора: все активные чужой валюты — подсвечены и названы своей
    /// валютой; один счёт — по имени; несколько — числом; удалённый не считается.
    /// </summary>
    [Fact]
    public async Task Строка_своего_набора_называет_его()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Вклад", 10_000m, excluded: true);
        Guid empty = await given.AccountAsync("Пустой", excluded: true);
        await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);

        IReadOnlyList<ReportAccount> all = await given.Database.Resolve<IReportAccountsQuery>().ReadAsync();

        ReportAccountsLine euro = ReportAccountsLine.Of(ReportAccounts.AllActive(Currency.EUR), all);
        Assert.Equal(string.Format(UiCulture.Current, UiTexts.ReportAccountsActive, Currency.EUR.Symbol), euro.Title);
        Assert.True(euro.IsCustom);

        Assert.Equal("Вклад", ReportAccountsLine.Of(ReportAccounts.Chosen(Currency.RUB, [savings]), all).Title);

        ReportAccounts three = ReportAccounts.Chosen(Currency.RUB, [card, savings, empty]);
        Assert.Equal("3 счёта", ReportAccountsLine.Of(three, all).Title);

        await given.Database.Resolve<IDeleteAccountHandler>().HandleAsync(empty);
        IReadOnlyList<ReportAccount> after = await given.Database.Resolve<IReportAccountsQuery>().ReadAsync();

        ReportAccountsLine line = ReportAccountsLine.Of(three, after);
        Assert.Equal("2 счёта", line.Title);
        Assert.Equal(2, line.Tokens.Count);
    }

    /// <summary>
    /// Выбранных счетов не осталось — удалён единственный: отчёт возвращается
    /// к умолчанию, а не показывает пустоту с подписью «0 счетов».
    /// </summary>
    [Fact]
    public async Task Набор_без_счетов_возвращается_к_умолчанию()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid empty = await given.AccountAsync("Пустой", excluded: true);

        await given.SaveAsync(given.Expense(card, 300m));

        ReportChoice choice = given.Database.Resolve<ReportChoice>();
        choice.Accounts = ReportAccounts.Chosen(Currency.RUB, [empty]);

        await given.Database.Resolve<IDeleteAccountHandler>().HandleAsync(empty);

        ReportViewModel report = await LoadedAsync(given);

        Assert.Same(ReportAccounts.Default, choice.Accounts);
        Assert.False(report.Accounts.IsCustom);
        Assert.Equal(Money.Restore(-300m, Currency.RUB).DisplaySigned, report.Total);
    }

    /// <summary>
    /// «Все активные» другой валюты без единого активного счёта — единственный удалён —
    /// тоже возвращаются к умолчанию: «Активные счета · €» над пустотой ни о чём.
    /// </summary>
    [Fact]
    public async Task Все_активные_без_счетов_возвращаются_к_умолчанию()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid euro = await given.AccountAsync("Карта евро", currency: Currency.EUR);

        await given.SaveAsync(given.Expense(card, 300m));

        ReportChoice choice = given.Database.Resolve<ReportChoice>();
        choice.Accounts = ReportAccounts.AllActive(Currency.EUR);

        await given.Database.Resolve<IDeleteAccountHandler>().HandleAsync(euro);

        ReportViewModel report = await LoadedAsync(given);

        Assert.Same(ReportAccounts.Default, choice.Accounts);
        Assert.Equal(Money.Restore(-300m, Currency.RUB).DisplaySigned, report.Total);
    }

    /// <summary>
    /// Перечитанный отчёт с тем же набором строку счетов не подменяет: подмена
    /// пересоздавала бы знаки на каждую смену месяца.
    /// </summary>
    [Fact]
    public async Task Строка_счетов_не_подменяется_тем_же_набором()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 10_000m);

        ReportViewModel report = await LoadedAsync(given);
        List<string?> changed = [];
        report.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await report.LoadAsync();

        Assert.DoesNotContain(nameof(ReportViewModel.Accounts), changed);
    }

    /// <summary>
    /// Больше пяти счетов — четыре знака и «+N»: «+1» на месте пятого знака не сэкономил бы места.
    /// </summary>
    [Fact]
    public async Task Лишние_знаки_сворачиваются_в_число()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        List<Guid> keys = [];

        for (int i = 1; i <= 6; i++)
        {
            keys.Add(await given.AccountAsync($"Счёт {i}"));
        }

        IReadOnlyList<ReportAccount> all = await given.Database.Resolve<IReportAccountsQuery>().ReadAsync();

        ReportAccountsLine six = ReportAccountsLine.Of(ReportAccounts.AllActive(Currency.RUB), all);
        Assert.Equal(4, six.Tokens.Count);
        Assert.Equal("+2", six.More);

        ReportAccountsLine five = ReportAccountsLine.Of(ReportAccounts.Chosen(Currency.RUB, keys[..5]), all);
        Assert.Equal(5, five.Tokens.Count);
        Assert.False(five.HasMore);
    }

    /// <summary>
    /// Уровни глубже показывают знаки только при своём наборе — и тогда те же, что строка первого уровня.
    /// </summary>
    [Fact]
    public async Task Знаки_глубже_только_при_своём_наборе()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Вклад", 10_000m, excluded: true);

        await given.SaveAsync(given.Expense(card, 300m));

        ReportTotal group = Assert.Single(await given.ReportAsync());

        ReportGroupViewModel plain = given.Database.Resolve<ReportGroupViewModel>();
        await plain.LoadAsync(group.Key, ReportMonth.Of(given.Today));

        Assert.False(plain.Accounts.IsCustom);

        given.Database.Resolve<ReportChoice>().Accounts = ReportAccounts.Chosen(Currency.RUB, [card, savings]);

        ReportGroupViewModel custom = given.Database.Resolve<ReportGroupViewModel>();
        await custom.LoadAsync(group.Key, ReportMonth.Of(given.Today));

        ReportSubcategoryViewModel third = given.Database.Resolve<ReportSubcategoryViewModel>();
        await third.LoadAsync(given.ExpenseCategory, ReportMonth.Of(given.Today));

        Assert.True(custom.Accounts.IsCustom);
        Assert.Equal(2, custom.Accounts.Tokens.Count);
        Assert.True(third.Accounts.IsCustom);
        Assert.Equal(2, third.Accounts.Tokens.Count);
    }

    /// <summary>
    /// Экран выбора при умолчании: отмечены активные рубли, у рублей — «Выбраны»,
    /// у евро — «Все активные», у валюты одних накоплений кнопки нет.
    /// </summary>
    [Fact]
    public async Task Выбор_открывается_с_отметками_умолчания()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 10_000m);
        await given.AccountAsync("Вклад", 10_000m, excluded: true);
        await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);
        await given.AccountAsync("Доллары дома", 1_000m, Currency.USD, excluded: true);

        ReportAccountsViewModel picker = await PickerAsync(given);

        ReportAccountsSection rubles = Section(picker, Currency.RUB);

        Assert.Equal(["Карта", "Вклад"], rubles.Select(static row => row.Name));
        Assert.Equal([true, false], rubles.Select(static row => row.IsChecked));
        Assert.Equal(UiTexts.PickAccountSavings, rubles[1].Caption);
        Assert.True(rubles.IsAllActive);
        Assert.False(rubles.OffersAllActive);

        Assert.True(Section(picker, Currency.EUR).OffersAllActive);
        Assert.False(Section(picker, Currency.USD).OffersAllActive);
    }

    /// <summary>
    /// Отметка накоплений даёт свой набор, снятие её — снова умолчание: набор, равный
    /// активным, хранится правилом, и подсветка не загорается зря.
    /// </summary>
    [Fact]
    public async Task Отметка_вклада_даёт_свой_набор_а_снятие_умолчание()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Вклад", 10_000m, excluded: true);

        ReportAccountsViewModel picker = await PickerAsync(given);
        ReportChoice choice = given.Database.Resolve<ReportChoice>();
        ReportAccountsSection rubles = Section(picker, Currency.RUB);

        picker.Toggle(rubles[1]);

        Assert.False(choice.Accounts.IsAllActive);
        // Множеством, а не по порядку: два счёта, заведённые в одну миллисекунду,
        // получают ключи, чей порядок задаёт случайная часть
        Assert.True(choice.Accounts.Keys!.SetEquals([card, savings]));
        Assert.True(rubles.OffersAllActive);

        picker.Toggle(rubles[1]);

        Assert.Same(ReportAccounts.Default, choice.Accounts);
        Assert.True(rubles.IsAllActive);
    }

    /// <summary>
    /// Счёт чужой валюты при отмеченных рублях не отмечается: всплывает сообщение,
    /// выбор не меняется.
    /// </summary>
    [Fact]
    public async Task Счёт_чужой_валюты_не_отмечается()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 10_000m);
        await given.AccountAsync("Доллары дома", 1_000m, Currency.USD, excluded: true);

        ReportAccountsViewModel picker = await PickerAsync(given);
        List<string> notices = [];
        picker.Notified += notices.Add;

        ReportAccountRow dollars = Assert.Single(Section(picker, Currency.USD));
        picker.Toggle(dollars);

        Assert.False(dollars.IsChecked);
        Assert.Equal([UiTexts.ReportAccountsOneCurrency], notices);
        Assert.Same(ReportAccounts.Default, given.Database.Resolve<ReportChoice>().Accounts);
    }

    /// <summary>
    /// «Все активные» у евро заменяет выбор целиком: рублёвые отметки снимаются,
    /// отчёт переходит на евро одним касанием; у рублей та же кнопка возвращает умолчание.
    /// </summary>
    [Fact]
    public async Task Все_активные_переводят_отчёт_в_другую_валюту()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 10_000m);
        await given.AccountAsync("Вклад", 10_000m, excluded: true);
        await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);

        ReportAccountsViewModel picker = await PickerAsync(given);
        ReportChoice choice = given.Database.Resolve<ReportChoice>();
        ReportAccountsSection rubles = Section(picker, Currency.RUB);
        ReportAccountsSection euro = Section(picker, Currency.EUR);

        picker.Toggle(rubles[1]);
        picker.ChooseAllActive(euro);

        Assert.True(choice.Accounts.IsAllActive);
        Assert.Equal(Currency.EUR, choice.Accounts.Currency);
        Assert.All(rubles, static row => Assert.False(row.IsChecked));
        Assert.True(euro.IsAllActive);

        picker.ChooseAllActive(rubles);

        Assert.Same(ReportAccounts.Default, choice.Accounts);
        Assert.Equal([true, false], rubles.Select(static row => row.IsChecked));
    }

    /// <summary>
    /// Снятые все отметки — отчёт по умолчанию, а экран остаётся без отметок:
    /// следующим касанием отмечается счёт любой валюты.
    /// </summary>
    [Fact]
    public async Task Снятые_отметки_открывают_другую_валюту()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        await given.AccountAsync("Карта", 10_000m);
        Guid dollars = await given.AccountAsync("Доллары дома", 1_000m, Currency.USD, excluded: true);

        ReportAccountsViewModel picker = await PickerAsync(given);
        ReportChoice choice = given.Database.Resolve<ReportChoice>();
        ReportAccountsSection rubles = Section(picker, Currency.RUB);

        picker.Toggle(rubles[0]);

        Assert.Same(ReportAccounts.Default, choice.Accounts);
        Assert.False(rubles[0].IsChecked);

        picker.Toggle(Assert.Single(Section(picker, Currency.USD)));

        Assert.Equal(Currency.USD, choice.Accounts.Currency);
        Assert.Equal([dollars], choice.Accounts.Keys!);
    }

    /// <summary>
    /// Суммы по набору ключей не превращаются в полный проход по таблице операций:
    /// список ключей обязан уйти в базу условием, а не отсеиваться в памяти.
    /// </summary>
    [Fact]
    public async Task Суммы_по_набору_не_идут_полным_проходом()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Вклад", 10_000m, excluded: true);

        ReportMonth month = ReportMonth.Of(given.Today);
        ReportAccounts accounts = ReportAccounts.Chosen(Currency.RUB, [card, savings]);

        // Все три уровня: условие по набору у них общее, но соединения вокруг
        // него разные, и план у каждого свой
        string[] queries =
        [
            ReportQuery.Groups(context, month, accounts).ToQueryString(),
            ReportQuery.Subcategories(context, given.ExpenseCategory, month, accounts).ToQueryString(),
            ReportQuery.Transactions(context, given.ExpenseCategory, month, accounts).ToQueryString()
        ];

        foreach (string sql in queries)
        {
            string alias = TransactionsAlias().Match(sql).Groups[1].Value;
            string plan = await QueryPlan.ExplainAsync(given.Database, sql);

            QueryPlan.NoFullScan(plan, alias);
        }
    }

    [GeneratedRegex("\"transactions\" AS \"(\\w+)\"")]
    private static partial Regex TransactionsAlias();

    private static async Task<ReportViewModel> LoadedAsync(TransactionFixture given)
    {
        ReportViewModel model = given.Database.Resolve<ReportViewModel>();

        await model.LoadAsync();

        return model;
    }

    private static async Task<ReportAccountsViewModel> PickerAsync(TransactionFixture given)
    {
        ReportAccountsViewModel picker = given.Database.Resolve<ReportAccountsViewModel>();

        await picker.LoadAsync();

        return picker;
    }

    private static ReportAccountsSection Section(ReportAccountsViewModel picker, Currency currency) =>
        picker.Sections.Single(section => section.Currency == currency);
}

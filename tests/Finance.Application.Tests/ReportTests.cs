using System.Text.RegularExpressions;
using Finance.Application.Features.Report;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>Отчёт за месяц: что входит в суммы, порядок, доли, границы месяца, экран.</summary>
public sealed partial class ReportTests
{
    /// <summary>Перевод — не расход и не доход: ни строки, ни суммы ни в одном виде.</summary>
    [Fact]
    public async Task Перевод_в_отчёт_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid cash = await given.AccountAsync("Наличные");

        await given.SaveAsync(given.Transfer(card, cash, 5_000m));

        Assert.Empty(await given.ReportAsync());
    }

    /// <summary>Расход по скрытому из расчётов счёту не меняет ни сумму группы, ни итог; по обычному — меняет.</summary>
    [Fact]
    public async Task Счёт_скрытый_из_расчётов_в_отчёт_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Копилка", 10_000m, excluded: true);

        await given.SaveAsync(given.Expense(card, 300m));
        await given.SaveAsync(given.Expense(savings, 700m));

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.Equal(Money.Restore(300m, Currency.RUB), group.Total);
    }

    /// <summary>Валютная операция в отчёт не входит вовсе — в отличие от ленты, где она видна.</summary>
    [Fact]
    public async Task Счёт_в_чужой_валюте_в_отчёт_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);

        await given.SaveAsync(given.Expense(euro, 48m));

        Assert.Empty(await given.ReportAsync());
        Assert.Single((await given.FeedAsync()).Items);
    }

    /// <summary>Служебная «Разница» из стартового набора помечена «вне отчётов» и в суммы не попадает.</summary>
    [Fact]
    public async Task Подкатегория_вне_отчётов_в_суммы_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid adjustment = await ServiceSubcategoryAsync(given, CategoryKind.Expense);

        await given.SaveAsync(given.Expense(card, 100m, category: adjustment));
        await given.SaveAsync(given.Expense(card, 250m));

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.Equal(Money.Restore(250m, Currency.RUB), group.Total);
    }

    /// <summary>Флаг, поставленный группе, а не подкатегории, убирает из отчёта все её подкатегории целиком.</summary>
    [Fact]
    public async Task Группа_вне_отчётов_в_суммы_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid hidden = await given.GroupAsync("Скрытая", CategoryKind.Expense);
        Guid subcategory = await given.SubcategoryAsync(hidden, "Внутри скрытой");

        await given.ExcludeFromReportsAsync(hidden);

        await given.SaveAsync(given.Expense(card, 100m, category: subcategory));
        await given.SaveAsync(given.Expense(card, 250m));

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.NotEqual(hidden, group.Key);
        Assert.Equal(Money.Restore(250m, Currency.RUB), group.Total);
    }

    /// <summary>Мягко удалённая операция уходит из сумм.</summary>
    [Fact]
    public async Task Удалённая_операция_в_отчёт_не_попадает()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);

        Guid deleted = await given.SaveAsync(given.Expense(card, 100m));
        await given.SaveAsync(given.Expense(card, 250m));

        await given.Database.Resolve<IDeleteTransactionHandler>().HandleAsync(deleted);

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.Equal(Money.Restore(250m, Currency.RUB), group.Total);
    }

    /// <summary>Порядок задаёт база: от большей суммы к меньшей.</summary>
    [Fact]
    public async Task Группы_идут_по_убыванию_суммы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid small = await SubcategoryOfNewGroupAsync(given, "Малая");
        Guid big = await SubcategoryOfNewGroupAsync(given, "Большая");
        Guid middle = await SubcategoryOfNewGroupAsync(given, "Средняя");

        await given.SaveAsync(given.Expense(card, 10m, category: small));
        await given.SaveAsync(given.Expense(card, 300m, category: big));
        await given.SaveAsync(given.Expense(card, 20m, category: middle));

        IReadOnlyList<ReportTotal> groups = await given.ReportAsync();

        Assert.Equal(["Большая", "Средняя", "Малая"], groups.Select(group => group.Name));
    }

    /// <summary>Точное значение в рублях: сумма поверх конвертера копеек считает то, что ожидается.</summary>
    [Fact]
    public async Task Итог_месяца_равен_сумме_групп()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid other = await SubcategoryOfNewGroupAsync(given, "Другая");

        await given.SaveAsync(given.Expense(card, 1_250.75m));
        await given.SaveAsync(given.Expense(card, 0.25m));
        await given.SaveAsync(given.Expense(card, 99.99m, category: other));

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.Equal(Money.Restore(-1_350.99m, Currency.RUB).DisplaySigned, model.Total);
        Assert.Equal(2, model.Rows.Count);
    }

    /// <summary>Доля группы — от итога месяца по выбранному виду, целыми процентами.</summary>
    [Fact]
    public async Task Доли_групп_считаются_от_итога_месяца()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid other = await SubcategoryOfNewGroupAsync(given, "Другая");

        await given.SaveAsync(given.Expense(card, 750m));
        await given.SaveAsync(given.Expense(card, 250m, category: other));

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.Equal(["75%", "25%"], model.Rows.Select(row => row.Share));
        Assert.Equal(0.75d, model.Rows[0].Fraction, precision: 6);
    }

    /// <summary>Доход не попадает в список расходов и наоборот; итог у каждого вида свой.</summary>
    [Fact]
    public async Task Расходы_и_доходы_разведены_по_видам()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);

        await given.SaveAsync(given.Expense(card, 300m));
        await given.SaveAsync(given.Income(card, 90_000m));

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.Equal(Money.Restore(-300m, Currency.RUB).DisplaySigned, model.Total);
        Assert.Single(model.Rows);

        model.Kind = CategoryKind.Income;

        Assert.Equal(Money.Restore(90_000m, Currency.RUB).DisplaySigned, model.Total);
        Assert.Single(model.Rows);
        Assert.Equal("100%", model.Rows[0].Share);
    }

    /// <summary>Обе граничные даты прошедшего месяца входят в его сумму.</summary>
    [Fact]
    public async Task Операции_первого_и_последнего_числа_в_месяц_входят()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        ReportMonth month = ReportMonth.Of(given.PreviousMonth);

        await given.SaveAsync(given.Expense(card, 1m, on: month.First));
        await given.SaveAsync(given.Expense(card, 2m, on: month.Last));

        ReportTotal group = Assert.Single(await given.ReportAsync(month));

        Assert.Equal(Money.Restore(3m, Currency.RUB), group.Total);
        Assert.Equal(month, ReportMonth.Of(month.Last));
    }

    /// <summary>Последнее число предыдущего и первое число следующего в суммы не попадают.</summary>
    [Fact]
    public async Task Операции_соседних_месяцев_в_месяц_не_входят()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        ReportMonth month = ReportMonth.Of(given.PreviousMonth);

        await given.SaveAsync(given.Expense(card, 1m, on: month.First.AddDays(-1)));
        await given.SaveAsync(given.Expense(card, 2m, on: month.Last.AddDays(1)));

        Assert.Empty(await given.ReportAsync(month));
    }

    /// <summary>По умолчанию — месяц сегодняшней даты пользователя и расходы.</summary>
    [Fact]
    public async Task Отчёт_открывается_на_текущем_месяце_с_расходами()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.Equal(ReportMonth.Of(given.Today), model.Month);
        Assert.Equal(CategoryKind.Expense, model.Kind);
        Assert.Equal(0, model.KindIndex);
    }

    /// <summary>Вперёд с текущего месяца идти некуда; после шага назад — можно, и ровно на один шаг.</summary>
    [Fact]
    public async Task Стрелка_вперёд_гаснет_на_текущем_месяце()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.False(model.CanGoForward);
        Assert.False(model.NextMonthCommand.CanExecute(null));

        await model.PreviousMonthCommand.ExecuteAsync(null);

        Assert.True(model.CanGoForward);
        Assert.Equal(ReportMonth.Of(given.PreviousMonth), model.Month);

        await model.NextMonthCommand.ExecuteAsync(null);

        Assert.False(model.CanGoForward);
        Assert.Equal(ReportMonth.Of(given.Today), model.Month);
    }

    /// <summary>Пустое состояние считается после отбора по виду: доходы есть, а расходов нет.</summary>
    [Fact]
    public async Task Месяц_без_операций_показывает_пустое_состояние()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта");

        await given.SaveAsync(given.Income(card, 100m));

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.True(model.IsEmpty);
        Assert.False(model.HasItems);
        Assert.Empty(model.Rows);
        Assert.Equal("В этом месяце трат не было", model.EmptyTitle);

        model.Kind = CategoryKind.Income;

        Assert.False(model.IsEmpty);
    }

    /// <summary>Переключатель видов пересобирает список из прочитанного: обращение к базе одно.</summary>
    [Fact]
    public async Task Переключение_вида_не_ходит_в_базу()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        CountingReport report = new(given.Database.Resolve<IReportQuery>());
        ReportViewModel model = new(report, given.Database.Resolve<IClock>(), given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync();
        model.Kind = CategoryKind.Income;
        model.Kind = CategoryKind.Expense;

        Assert.Equal(1, report.Reads);
    }

    /// <summary>Сложение, уехавшее в память, на маленьких данных ничем себя не выдаст — сверяется сам SQL.</summary>
    [Fact]
    public async Task Суммы_месяца_считает_база()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        string sql = ReportQuery.Groups(context, ReportMonth.Of(given.Today)).ToQueryString();

        Assert.Contains("SUM(", sql, StringComparison.Ordinal);
        Assert.Contains("GROUP BY", sql, StringComparison.Ordinal);
        Assert.Matches(@"ORDER BY .*SUM\(.*DESC", sql);
    }

    /// <summary>
    /// Суммы месяца не превращаются в полный проход по таблице операций. Имя индекса
    /// не сверяется: планировщик вправе пойти и от счетов, и оба плана хороши —
    /// плохо ровно одно, полный проход. Псевдоним таблицы EF выбирает сам, поэтому
    /// он вынимается из SQL, а не угадывается.
    /// </summary>
    [Fact]
    public async Task Суммы_месяца_не_идут_полным_проходом()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        string sql = ReportQuery.Groups(context, ReportMonth.Of(given.Today)).ToQueryString();
        string alias = TransactionsAlias().Match(sql).Groups[1].Value;

        string plan = await QueryPlan.ExplainAsync(given.Database, sql);

        Assert.False(string.IsNullOrEmpty(alias), sql);
        Assert.DoesNotContain($"SCAN {alias}\n", plan.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [GeneratedRegex("\"transactions\" AS \"(\\w+)\"")]
    private static partial Regex TransactionsAlias();

    private static async Task<ReportViewModel> LoadedModelAsync(TransactionFixture given)
    {
        ReportViewModel model = new(
            given.Database.Resolve<IReportQuery>(),
            given.Database.Resolve<IClock>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        return model;
    }

    private static async Task<Guid> SubcategoryOfNewGroupAsync(TransactionFixture given, string name)
    {
        Guid group = await given.GroupAsync(name, CategoryKind.Expense);

        return await given.SubcategoryAsync(group, name);
    }

    /// <summary>Служебная подкатегория «Разница» нужного вида — та, что помечена «вне отчётов» в стартовом наборе.</summary>
    private static async Task<Guid> ServiceSubcategoryAsync(TransactionFixture given, CategoryKind kind)
    {
        IReadOnlyList<CategoryListItem> categories = await given.Database.Resolve<ICategoriesQuery>().ReadAsync();

        CategoryListItem group = categories.Single(item => item.IsGroup && item.Role is CategoryRole.Service && item.Kind == kind);

        return categories.Single(item => item.ParentKey == group.Key).Key;
    }

    /// <summary>Считает обращения к базе: пересборка списка в памяти не должна ходить за данными заново.</summary>
    private sealed class CountingReport(IReportQuery inner) : IReportQuery
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyList<ReportTotal>> ReadGroupsAsync(ReportMonth month, CancellationToken cancellationToken = default)
        {
            Reads++;

            return inner.ReadGroupsAsync(month, cancellationToken);
        }
    }
}

using System.Text.RegularExpressions;
using Finance.Application.Features.Report;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Deletion;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Отчёт за месяц: что входит в суммы, порядок, доли, границы месяца, экран.
/// </summary>
public sealed partial class ReportTests
{
    /// <summary>
    /// Перевод — не расход и не доход: ни строки, ни суммы ни в одном виде.
    /// </summary>
    [Fact]
    public async Task Перевод_в_отчёт_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid cash = await given.AccountAsync("Наличные");

        await given.SaveAsync(given.Transfer(card, cash, 5_000m));

        Assert.Empty(await given.ReportAsync());
    }

    /// <summary>
    /// Расход по скрытому счёту не меняет ни сумму группы, ни итог; по обычному — меняет.
    /// </summary>
    [Fact]
    public async Task Счёт_скрытый_в_отчёт_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid savings = await given.AccountAsync("Копилка", 10_000m, excluded: true);

        await given.SaveAsync(given.Expense(card, 300m));
        await given.SaveAsync(given.Expense(savings, 700m));

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.Equal(Money.Restore(300m, Currency.RUB), group.Total);
    }

    /// <summary>
    /// Валютная операция в отчёт не входит вовсе — в отличие от ленты, где она видна.
    /// </summary>
    [Fact]
    public async Task Счёт_в_чужой_валюте_в_отчёт_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);

        await given.SaveAsync(given.Expense(euro, 48m));

        Assert.Empty(await given.ReportAsync());
        Assert.Single((await given.FeedAsync()).Items);
    }

    /// <summary>
    /// Служебная «Разница» из стартового набора помечена «вне отчётов» и в суммы не попадает.
    /// </summary>
    [Fact]
    public async Task Подкатегория_вне_отчётов_в_суммы_не_входит()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid adjustment = await ServiceSubcategoryAsync(given);

        await given.SaveAsync(given.Expense(card, 100m, category: adjustment));
        await given.SaveAsync(given.Expense(card, 250m));

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.Equal(Money.Restore(250m, Currency.RUB), group.Total);
    }

    /// <summary>
    /// Флаг, поставленный группе, а не подкатегории, убирает из отчёта все её подкатегории целиком.
    /// </summary>
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

    /// <summary>
    /// Мягко удалённая операция уходит из сумм.
    /// </summary>
    [Fact]
    public async Task Удалённая_операция_в_отчёт_не_попадает()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);

        Guid deleted = await given.SaveAsync(given.Expense(card, 100m));
        await given.SaveAsync(given.Expense(card, 250m));

        await given.Database.Resolve<IDeleteTransactionsHandler>().HandleAsync([deleted]);

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.Equal(Money.Restore(250m, Currency.RUB), group.Total);
    }

    /// <summary>
    /// Порядок задаёт база: от большей суммы к меньшей.
    /// </summary>
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

    /// <summary>
    /// Возврат в универсальной расходной группе вычитается из её расхода, а не
    /// прибавляется к нему: иначе возврат читался бы как ещё одна трата.
    /// </summary>
    [Fact]
    public async Task Возврат_вычитается_из_расхода_универсальной_группы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid shopping = await SubcategoryOfNewGroupAsync(given, "Маркетплейсы", acceptsAnyKind: true);

        await given.SaveAsync(given.Expense(card, 1_000m, category: shopping));
        await given.SaveAsync(given.Income(card, 300m, category: shopping));

        ReportTotal group = Assert.Single(await given.ReportAsync());

        Assert.Equal(Money.Restore(700m, Currency.RUB), group.Total);
    }

    /// <summary>
    /// Порядок строк считается по сведённому итогу, а не по одной из двух сумм:
    /// группа с крупным возвратом обязана съехать вниз.
    /// </summary>
    [Fact]
    public async Task Группы_идут_по_убыванию_сведённого_итога()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid returned = await SubcategoryOfNewGroupAsync(given, "С возвратом", acceptsAnyKind: true);
        Guid plain = await SubcategoryOfNewGroupAsync(given, "Без возврата");

        await given.SaveAsync(given.Expense(card, 1_000m, category: returned));
        await given.SaveAsync(given.Income(card, 900m, category: returned));
        await given.SaveAsync(given.Expense(card, 500m, category: plain));

        IReadOnlyList<ReportTotal> groups = await given.ReportAsync();

        Assert.Equal(["Без возврата", "С возвратом"], groups.Select(group => group.Name));
    }

    /// <summary>
    /// Группа, где возвратов за месяц больше, чем трат, показывает сумму плюсом
    /// цветом дохода и без доли; доли остальных считаются без неё и сходятся к сотне.
    /// </summary>
    [Fact]
    public async Task Группа_в_минусе_от_возвратов_показывается_без_доли()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid returned = await SubcategoryOfNewGroupAsync(given, "Только возврат", acceptsAnyKind: true);
        Guid other = await SubcategoryOfNewGroupAsync(given, "Обычная");

        await given.SaveAsync(given.Income(card, 300m, category: returned));
        await given.SaveAsync(given.Expense(card, 1_000m, category: other));

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.Equal(["100%", string.Empty], model.Rows.Select(row => row.Share));

        ReportRowItem refund = model.Rows[1];

        Assert.Equal("Только возврат", refund.Name);
        Assert.Equal(Money.Restore(300m, Currency.RUB).DisplaySigned, refund.Amount);
        Assert.False(refund.IsExpense);
        Assert.False(refund.HasShare);
        Assert.Equal(0d, refund.Fraction);

        // Итог — настоящая сумма: возврат и правда уменьшил расход месяца
        Assert.Equal(Money.Restore(-700m, Currency.RUB).DisplaySigned, model.Total);
        Assert.True(model.IsTotalExpense);
    }

    /// <summary>
    /// Месяц одних возвратов сводит расходы в плюс, и итог красится по знаку, а не по виду.
    /// </summary>
    [Fact]
    public async Task Итог_из_одних_возвратов_красится_по_знаку()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid returned = await SubcategoryOfNewGroupAsync(given, "Только возврат", acceptsAnyKind: true);

        await given.SaveAsync(given.Income(card, 300m, category: returned));

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.Equal(Money.Restore(300m, Currency.RUB).DisplaySigned, model.Total);
        Assert.False(model.IsTotalExpense);
        Assert.False(Assert.Single(model.Rows).HasShare);
    }

    /// <summary>
    /// На втором уровне то же: шапка группы в минусе не называет долю месяца.
    /// </summary>
    [Fact]
    public async Task Шапка_группы_в_минусе_не_называет_долю()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid group = await given.GroupAsync("Только возврат", CategoryKind.Expense, acceptsAnyKind: true);
        Guid subcategory = await given.SubcategoryAsync(group, "Возвраты");

        await given.SaveAsync(given.Income(card, 300m, category: subcategory));
        await given.SaveAsync(given.Expense(card, 1_000m));

        ReportGroupViewModel model = new(given.Database.Resolve<IReportQuery>(), given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(group, ReportMonth.Of(given.Today));

        Assert.Equal(ReportMonth.Of(given.Today).Caption, model.Caption);
        Assert.False(model.IsTotalExpense);
        Assert.False(Assert.Single(model.Rows).HasShare);
    }

    /// <summary>
    /// Точное значение в рублях: сумма поверх конвертера копеек считает то, что ожидается.
    /// </summary>
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

    /// <summary>
    /// Доля группы — от итога месяца по выбранному виду, целыми процентами.
    /// </summary>
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

    /// <summary>
    /// Доход не попадает в список расходов и наоборот; итог у каждого вида свой.
    /// </summary>
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

        Assert.True(model.IsTotalExpense);
        Assert.True(model.Rows[0].IsExpense);

        model.Kind = CategoryKind.Income;

        Assert.Equal(Money.Restore(90_000m, Currency.RUB).DisplaySigned, model.Total);
        Assert.Single(model.Rows);
        Assert.Equal("100%", model.Rows[0].Share);

        // Знак у сумм разный, и цвет обязан идти за ним: иначе доход и расход
        // на одном экране различаются только минусом
        Assert.False(model.IsTotalExpense);
        Assert.False(model.Rows[0].IsExpense);
    }

    /// <summary>
    /// Обе граничные даты прошедшего месяца входят в его сумму.
    /// </summary>
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

    /// <summary>
    /// Последнее число предыдущего и первое число следующего в суммы не попадают.
    /// </summary>
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

    /// <summary>
    /// Последний день високосного февраля — ещё февраль, а первое марта — уже нет.
    /// </summary>
    [Fact]
    public async Task Двадцать_девятое_февраля_входит_в_февраль()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m, openedOn: new DateOnly(2024, 1, 1));
        ReportMonth february = ReportMonth.Of(new DateOnly(2024, 2, 1));

        Assert.Equal(new DateOnly(2024, 2, 29), february.Last);

        await given.SaveAsync(given.Expense(card, 1m, on: february.Last));
        await given.SaveAsync(given.Expense(card, 2m, on: new DateOnly(2024, 3, 1)));

        ReportTotal group = Assert.Single(await given.ReportAsync(february));

        Assert.Equal(Money.Restore(1m, Currency.RUB), group.Total);
    }

    /// <summary>
    /// По умолчанию — месяц сегодняшней даты пользователя и расходы.
    /// </summary>
    [Fact]
    public async Task Отчёт_открывается_на_текущем_месяце_с_расходами()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        ReportViewModel model = await LoadedModelAsync(given);

        Assert.Equal(ReportMonth.Of(given.Today), model.Month);
        Assert.Equal(CategoryKind.Expense, model.Kind);
    }

    /// <summary>
    /// Вперёд с текущего месяца идти некуда; после шага назад — можно, и ровно на один шаг.
    /// </summary>
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

    /// <summary>
    /// Пустое состояние считается после отбора по виду: доходы есть, а расходов нет.
    /// </summary>
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

        model.Kind = CategoryKind.Income;

        Assert.False(model.IsEmpty);
    }

    /// <summary>
    /// Пустой отчёт при валютном счёте не советует листать месяцы: операции
    /// в этом месяце есть, просто по счетам, которые в отчёт не входят.
    /// </summary>
    [Fact]
    public async Task Пустой_отчёт_при_валютном_счёте_не_советует_листать()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid euro = await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);

        ReportViewModel before = await LoadedModelAsync(given);
        Assert.Equal(UiTexts.ReportEmptyOtherMonth, before.EmptyHint);

        await given.SaveAsync(given.Expense(euro, 48m));

        ReportViewModel after = await LoadedModelAsync(given);

        Assert.True(after.IsEmpty);
        Assert.Equal(UiTexts.ReportEmptyCurrency, after.EmptyHint);
    }

    /// <summary>
    /// Переключатель видов пересобирает список из прочитанного: обращение к базе одно.
    /// </summary>
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

    /// <summary>
    /// Подкатегории чужой группы в список не попадают; сумма уровня равна строке группы с первого уровня.
    /// </summary>
    [Fact]
    public async Task Второй_уровень_показывает_подкатегории_своей_группы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid food = await given.GroupAsync("Питание", CategoryKind.Expense);
        Guid grocery = await given.SubcategoryAsync(food, "Продукты");
        Guid cafe = await given.SubcategoryAsync(food, "Кафе");

        await given.SaveAsync(given.Expense(card, 300m, category: grocery));
        await given.SaveAsync(given.Expense(card, 700m, category: cafe));
        await given.SaveAsync(given.Expense(card, 999m));

        IReadOnlyList<ReportTotal> rows = await given.Database.Resolve<IReportQuery>()
            .ReadSubcategoriesAsync(food, ReportMonth.Of(given.Today));
        ReportTotal group = (await given.ReportAsync()).Single(item => item.Key == food);

        Assert.Equal(["Кафе", "Продукты"], rows.Select(row => row.Name));
        Assert.Equal(group.Total, rows.Aggregate(Money.Zero(Currency.RUB), (sum, row) => sum + row.Total));
    }

    /// <summary>
    /// Доля подкатегории — от суммы группы, а не от месяца; шапка несёт долю группы в месяце.
    /// </summary>
    [Fact]
    public async Task Доли_подкатегорий_считаются_внутри_группы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid food = await given.GroupAsync("Питание", CategoryKind.Expense);
        Guid grocery = await given.SubcategoryAsync(food, "Продукты");
        Guid cafe = await given.SubcategoryAsync(food, "Кафе");

        // Питание — 40% месяца; внутри неё кафе — 75%, продукты — 25%
        await given.SaveAsync(given.Expense(card, 100m, category: grocery));
        await given.SaveAsync(given.Expense(card, 300m, category: cafe));
        await given.SaveAsync(given.Expense(card, 600m));

        ReportGroupViewModel model = new(given.Database.Resolve<IReportQuery>(), given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(food, ReportMonth.Of(given.Today));

        Assert.Equal("Питание", model.Name);
        Assert.Equal(["75%", "25%"], model.Rows.Select(row => row.Share));
        Assert.EndsWith("· 40% расходов", model.Caption, StringComparison.Ordinal);
        Assert.Equal(Money.Restore(-400m, Currency.RUB).DisplaySigned, model.Total);
        Assert.False(model.IsEmpty);
    }

    /// <summary>
    /// Строки не сгруппированы по дням; порядок — дата, момент записи, ключ; валютная операция отсутствует.
    /// </summary>
    [Fact]
    public async Task Третий_уровень_идёт_плоским_списком_от_новых_к_старым()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);
        Guid euro = await given.AccountAsync("Карта евро", 1_000m, Currency.EUR);
        DateOnly today = given.Today;

        Guid old = await given.SaveAsync(given.Expense(card, 1m, on: today.AddDays(-2), place: "Самокат"));
        Guid first = await given.SaveAsync(given.Expense(card, 2m, on: today));
        Guid second = await given.SaveAsync(given.Expense(card, 3m, on: today));
        await given.SaveAsync(given.Expense(euro, 4m, on: today));

        IReadOnlyList<ReportTransaction> items = await given.Database.Resolve<IReportQuery>()
            .ReadTransactionsAsync(given.ExpenseCategory, ReportMonth.Of(today));

        Assert.Equal([second, first, old], items.Select(item => item.Key));
        Assert.Equal("Самокат", items[^1].Place);
        Assert.Equal(Money.Restore(-1m, Currency.RUB), items[^1].Amount);
        Assert.Equal("Карта", items[^1].AccountName);
    }

    /// <summary>
    /// Шапка несёт склонённое число операций и итог со знаком; строка — ключ операции для карточки.
    /// </summary>
    [Fact]
    public async Task Шапка_третьего_уровня_считает_операции_и_сумму()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 10_000m);

        Guid saved = await given.SaveAsync(given.Expense(card, 890m, place: "Самокат"));
        await given.SaveAsync(given.Expense(card, 1_140m));

        ReportSubcategoryViewModel model = new(
            given.Database.Resolve<IReportQuery>(),
            given.Database.Resolve<ICategoriesQuery>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync(given.ExpenseCategory, ReportMonth.Of(given.Today));

        Assert.False(string.IsNullOrEmpty(model.Name));
        Assert.EndsWith("· 2 операции", model.Caption, StringComparison.Ordinal);
        Assert.Equal(Money.Restore(-2_030m, Currency.RUB).DisplaySigned, model.Total);
        Assert.Equal(2, model.Rows.Count);

        ReportTransactionItem row = model.Rows.Single(item => item.Key == saved);

        Assert.Equal("Самокат", row.Title);
        Assert.EndsWith("· Карта", row.Caption, StringComparison.Ordinal);

        // Без места заголовком идёт сама подкатегория
        Assert.Equal(model.Name, model.Rows.Single(item => item.Key != saved).Title);
    }

    /// <summary>
    /// Третий уровень идёт по индексу подкатегории — тому, что заведён под отчёт.
    /// </summary>
    [Fact]
    public async Task Операции_подкатегории_идут_по_своему_индексу()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        string plan = await QueryPlan.ExplainAsync(
            given.Database,
            ReportQuery.Transactions(context, given.ExpenseCategory, ReportMonth.Of(given.Today)));

        Assert.Contains("ix_transactions_category_occurred_on", plan, StringComparison.Ordinal);
    }

    /// <summary>
    /// Сложение, уехавшее в память, на маленьких данных ничем себя не выдаст — сверяется сам SQL.
    /// Группировка идёт и по виду операции: у универсальной группы лежат оба вида,
    /// и свести их со знаком можно только двумя суммами. Порядок строк поэтому
    /// считается уже по сведённому итогу, в памяти, — его сверяет
    /// <see cref="Группы_идут_по_убыванию_сведённого_итога"/>.
    /// </summary>
    [Fact]
    public async Task Суммы_месяца_считает_база()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        string sql = ReportQuery.Groups(context, ReportMonth.Of(given.Today)).ToQueryString();
        string alias = TransactionsAlias().Match(sql).Groups[1].Value;
        string groupBy = GroupByClause().Match(sql).Groups[1].Value;

        Assert.Contains("SUM(", sql, StringComparison.Ordinal);

        // Слово kind в SQL есть и без группировки по нему — у колонки группы.
        // Сверяется именно вид операции в списке GROUP BY
        Assert.False(string.IsNullOrEmpty(alias), sql);
        Assert.Contains($"\"{alias}\".\"kind\"", groupBy, StringComparison.Ordinal);
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

    [GeneratedRegex("^GROUP BY (.+)$", RegexOptions.Multiline)]
    private static partial Regex GroupByClause();

    private static async Task<ReportViewModel> LoadedModelAsync(TransactionFixture given)
    {
        ReportViewModel model = new(
            given.Database.Resolve<IReportQuery>(),
            given.Database.Resolve<IClock>(),
            given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        return model;
    }

    private static async Task<Guid> SubcategoryOfNewGroupAsync(
        TransactionFixture given,
        string name,
        bool acceptsAnyKind = false)
    {
        Guid group = await given.GroupAsync(name, CategoryKind.Expense, acceptsAnyKind);

        return await given.SubcategoryAsync(group, name);
    }

    /// <summary>
    /// Служебная подкатегория «Разница» — та, что помечена «вне отчётов» в стартовом наборе.
    /// Она одна на оба вида: служебная группа универсальна.
    /// </summary>
    private static async Task<Guid> ServiceSubcategoryAsync(TransactionFixture given)
    {
        IReadOnlyList<CategoryListItem> categories = await given.Database.Resolve<ICategoriesQuery>().ReadAsync();

        CategoryListItem group = categories.Single(item => item.IsGroup && item.Role is CategoryRole.Service);

        return categories.Single(item => item.ParentKey == group.Key).Key;
    }

    /// <summary>
    /// Быстрое листание: первое чтение отстало и завершилось после второго.
    /// Его строки уже не к месту — под шапкой позапрошлого месяца обязаны стоять
    /// группы позапрошлого, а не прошлого.
    /// </summary>
    [Fact]
    public async Task Отставшее_чтение_не_перекрывает_свежий_месяц()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта");
        DateOnly monthBefore = given.PreviousMonth.AddMonths(-1);

        await given.SaveAsync(given.Expense(card, 300m, on: given.PreviousMonth));
        await given.SaveAsync(given.Expense(card, 700m, on: monthBefore));

        DelayingReport report = new(given.Database.Resolve<IReportQuery>());
        ReportViewModel model = new(report, given.Database.Resolve<IClock>(), given.Database.Resolve<IChangeNotifier>());

        await model.LoadAsync();

        // Первое листание задерживается, второе проходит сразу
        report.Delay = new TaskCompletionSource();
        Task slow = model.PreviousMonthCommand.ExecuteAsync(null);

        TaskCompletionSource hold = report.Delay;
        report.Delay = null;
        await model.PreviousMonthCommand.ExecuteAsync(null);

        hold.SetResult();
        await slow;

        Assert.Equal(ReportMonth.Of(monthBefore), model.Month);
        Assert.Equal(Money.Restore(-700m, Currency.RUB).DisplaySigned, model.Total);
    }

    /// <summary>
    /// Задерживает чтение групп по сигналу теста, чтобы подстроить гонку.
    /// </summary>
    private sealed class DelayingReport(IReportQuery inner) : IReportQuery
    {
        public TaskCompletionSource? Delay { get; set; }

        public async Task<IReadOnlyList<ReportTotal>> ReadGroupsAsync(ReportMonth month, CancellationToken cancellationToken = default)
        {
            if (Delay is { } delay)
            {
                await delay.Task;
            }

            return await inner.ReadGroupsAsync(month, cancellationToken);
        }

        public Task<IReadOnlyList<ReportTotal>> ReadSubcategoriesAsync(Guid groupKey, ReportMonth month, CancellationToken cancellationToken = default) =>
            inner.ReadSubcategoriesAsync(groupKey, month, cancellationToken);

        public Task<IReadOnlyList<ReportTransaction>> ReadTransactionsAsync(Guid subcategoryKey, ReportMonth month, CancellationToken cancellationToken = default) =>
            inner.ReadTransactionsAsync(subcategoryKey, month, cancellationToken);

        public Task<bool> HasUncountedAsync(ReportMonth month, CancellationToken cancellationToken = default) =>
            inner.HasUncountedAsync(month, cancellationToken);
    }

    /// <summary>
    /// Считает обращения к базе: пересборка списка в памяти не должна ходить за данными заново.
    /// </summary>
    private sealed class CountingReport(IReportQuery inner) : IReportQuery
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyList<ReportTotal>> ReadGroupsAsync(ReportMonth month, CancellationToken cancellationToken = default)
        {
            Reads++;

            return inner.ReadGroupsAsync(month, cancellationToken);
        }

        public Task<IReadOnlyList<ReportTotal>> ReadSubcategoriesAsync(Guid groupKey, ReportMonth month, CancellationToken cancellationToken = default)
        {
            Reads++;

            return inner.ReadSubcategoriesAsync(groupKey, month, cancellationToken);
        }

        public Task<IReadOnlyList<ReportTransaction>> ReadTransactionsAsync(Guid subcategoryKey, ReportMonth month, CancellationToken cancellationToken = default)
        {
            Reads++;

            return inner.ReadTransactionsAsync(subcategoryKey, month, cancellationToken);
        }

        // Подсказка пустого состояния читается вместе с группами и к переключателю
        // видов отношения не имеет — в счёт обращений не идёт
        public Task<bool> HasUncountedAsync(ReportMonth month, CancellationToken cancellationToken = default) =>
            inner.HasUncountedAsync(month, cancellationToken);
    }
}

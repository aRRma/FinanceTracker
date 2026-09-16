using System.Text.RegularExpressions;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Tests;

/// <summary>
/// Панель частых подкатегорий над списком выбора: чем она наполняется, что в неё
/// не попадает и как выбор с панели доезжает до формы операции.
/// </summary>
public sealed partial class FrequentCategoriesTests
{
    /// <summary>
    /// Порядок задан числом операций: чаще записывали — ближе к началу панели.
    /// </summary>
    [Fact]
    public async Task Частые_идут_от_частых_к_редким()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 100_000m);
        Guid group = await given.GroupAsync("Питомцы", CategoryKind.Expense);
        Guid food = await given.SubcategoryAsync(group, "Корм коту");
        Guid vet = await given.SubcategoryAsync(group, "Ветеринар");

        await given.SaveAsync(given.Expense(card, 300m, category: vet));

        for (int repeat = 0; repeat < 3; repeat++)
        {
            await given.SaveAsync(given.Expense(card, 100m, category: food));
        }

        CategoryPickerViewModel picker = Picker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.Equal(["Корм коту", "Ветеринар"], picker.Frequent.Select(line => line.Name));
        Assert.True(picker.IsFrequentVisible);
    }

    /// <summary>
    /// Окно частоты — последние месяцы: позапрошлая привычка панель не занимает.
    /// </summary>
    [Fact]
    public async Task Давние_операции_в_частые_не_попадают()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 100_000m, openedOn: given.Today.AddYears(-2));
        Guid group = await given.GroupAsync("Питомцы", CategoryKind.Expense);
        Guid old = await given.SubcategoryAsync(group, "Корм коту");
        Guid recent = await given.SubcategoryAsync(group, "Ветеринар");

        // Давняя подкатегория берёт числом, и без окна она стояла бы первой
        for (int repeat = 0; repeat < 5; repeat++)
        {
            await given.SaveAsync(given.Expense(card, 100m, on: given.Today.AddMonths(-5), category: old));
        }

        await given.SaveAsync(given.Expense(card, 300m, category: recent));

        CategoryPickerViewModel picker = Picker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.Equal(["Ветеринар"], picker.Frequent.Select(line => line.Name));
    }

    /// <summary>
    /// Вид категорий панели — вид операции: доходная подкатегория в расходной панели
    /// не появляется, как не появляется и в списке под ней.
    /// </summary>
    [Fact]
    public async Task Частые_другого_вида_не_показываются()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 100_000m);
        Guid group = await given.GroupAsync("Подработка", CategoryKind.Income);
        Guid side = await given.SubcategoryAsync(group, "Уроки");

        await given.SaveAsync(given.Income(card, 5_000m, category: side));
        await given.SaveAsync(given.Expense(card, 100m));

        CategoryPickerViewModel expenses = Picker(given);
        await expenses.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.DoesNotContain("Уроки", expenses.Frequent.Select(line => line.Name));

        CategoryPickerViewModel incomes = Picker(given);
        await incomes.LoadAsync(CategoryKind.Income, selected: null);

        Assert.Equal(["Уроки"], incomes.Frequent.Select(line => line.Name));
    }

    /// <summary>
    /// Пока операций нет, панели нет вовсе: пустая полоса над списком объясняла бы
    /// только то, что приложением ещё не пользовались.
    /// </summary>
    [Fact]
    public async Task Панели_нет_пока_нет_операций()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        CategoryPickerViewModel picker = Picker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.Empty(picker.Frequent);
        Assert.False(picker.IsFrequentVisible);
    }

    /// <summary>
    /// С первой буквой поиска панель уходит: набравший буквы ищет как раз не частое.
    /// </summary>
    [Fact]
    public async Task Поиск_прячет_панель()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 100_000m);
        await given.SaveAsync(given.Expense(card, 100m));

        CategoryPickerViewModel picker = Picker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        Assert.True(picker.IsFrequentVisible);

        picker.Filter = "Кор";

        Assert.False(picker.IsFrequentVisible);

        picker.Filter = string.Empty;

        Assert.True(picker.IsFrequentVisible);
    }

    /// <summary>
    /// Выбор с панели — тот же выбор, что и из списка: форма получает его так же.
    /// Стоящая в форме подкатегория помечена и на панели.
    /// </summary>
    [Fact]
    public async Task Выбор_с_панели_доезжает_до_формы()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();

        Guid card = await given.AccountAsync("Карта", 100_000m);
        Guid group = await given.GroupAsync("Питомцы", CategoryKind.Expense);
        Guid food = await given.SubcategoryAsync(group, "Корм коту");

        await given.SaveAsync(given.Expense(card, 100m, category: food));

        TransactionViewModel form = Form(given);
        await form.LoadAsync(key: null);

        CategoryPickerViewModel picker = Picker(given);
        await picker.LoadAsync(CategoryKind.Expense, selected: null);

        CategoryPickerLine chip = picker.Frequent.First(line => line.Key == food);
        picker.Pick(chip);
        form.ApplyPicks();

        Assert.Equal(food, form.Category?.Key);

        CategoryPickerViewModel again = Picker(given);
        await again.LoadAsync(CategoryKind.Expense, selected: food);

        Assert.True(again.Frequent.First(line => line.Key == food).IsSelected);
    }

    /// <summary>
    /// Частые считает база, и полным проходом по операциям это не делается:
    /// панель открывается на каждый расход, а операций за годы набираются тысячи.
    /// </summary>
    [Fact]
    public async Task Частые_не_идут_полным_проходом()
    {
        await using TransactionFixture given = await TransactionFixture.CreateAsync();
        await using FinanceDbContext context = await given.Database.Contexts.CreateDbContextAsync();

        string sql = FrequentCategoriesQuery
            .Frequent(context, CategoryKind.Expense, given.Today.AddMonths(-3), limit: 8)
            .ToQueryString();

        string alias = TransactionsAlias().Match(sql).Groups[1].Value;

        string plan = await QueryPlan.ExplainAsync(given.Database, sql);

        Assert.False(string.IsNullOrEmpty(alias), sql);
        Assert.DoesNotContain($"SCAN {alias}\n", plan.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [GeneratedRegex("\"transactions\" AS \"(\\w+)\"")]
    private static partial Regex TransactionsAlias();

    private static CategoryPickerViewModel Picker(TransactionFixture fixture) => new(
        fixture.Database.Resolve<ICategoriesQuery>(),
        fixture.Database.Resolve<IFrequentCategoriesQuery>(),
        fixture.Database.Resolve<TransactionPicks>());

    private static TransactionViewModel Form(TransactionFixture fixture) => new(
        fixture.Database.Resolve<ITransactionFormQuery>(),
        fixture.Database.Resolve<ITransactionCardQuery>(),
        fixture.Database.Resolve<ISaveTransactionHandler>(),
        fixture.Database.Resolve<IDeleteTransactionHandler>(),
        fixture.Database.Resolve<IAccountsQuery>(),
        fixture.Database.Resolve<IClock>(),
        fixture.Database.Resolve<TransactionPicks>());
}

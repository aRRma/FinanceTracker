using Finance.Application.Features.Balances;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using static Finance.Application.Tests.AccountSetup;

namespace Finance.Application.Tests;

/// <summary>
/// Главный экран: разделы по валютам, подытог и признаки состояния.
/// </summary>
public sealed class BalancesTests
{
    /// <summary>
    /// Минус на счёте помечен признаком, а не только знаком в тексте: экран красит
    /// такой баланс смысловым цветом, и различать его по минусу в строке нельзя.
    /// </summary>
    [Fact]
    public async Task Отрицательный_баланс_счёта_помечен()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid card = await SaveAsync(database, Command("Карта", 100m));
        Guid cash = await SaveAsync(database, Command("Наличные", 100m));

        await AddAsync(database, Expense(card, 250m));

        BalancesViewModel model = Model(database);
        await model.LoadAsync();

        CurrencySection section = Assert.Single(model.Sections);

        Assert.True(Tile(section, card).IsNegative);
        Assert.False(Tile(section, cash).IsNegative);
    }

    /// <summary>
    /// «Доступно к тратам» тоже уходит в минус — когда минус одного счёта больше
    /// остатка на прочих. Подытог считается по тратимым счетам, накопления его
    /// не спасают.
    /// </summary>
    [Fact]
    public async Task Отрицательный_подытог_помечен()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid card = await SaveAsync(database, Command("Карта", 100m));
        await SaveAsync(database, Command("Копилка", 5000m, excludedFromTotals: true));

        await AddAsync(database, Expense(card, 250m));

        BalancesViewModel model = Model(database);
        await model.LoadAsync();

        CurrencySection section = Assert.Single(model.Sections);

        Assert.True(section.IsAvailableNegative);
    }

    /// <summary>
    /// Валюта одних накоплений «доступно» не показывает: там оно всегда ноль. Появился
    /// нескрытый счёт той же валюты — подытог вернулся.
    /// </summary>
    [Fact]
    public async Task Валюта_одних_накоплений_не_показывает_доступно()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Command("Копилка", 5000m, excludedFromTotals: true));
        await SaveAsync(database, Command("Вклад", 1000m, excludedFromTotals: true));

        BalancesViewModel model = Model(database);
        await model.LoadAsync();

        Assert.False(Assert.Single(model.Sections).HasSpendable);

        await SaveAsync(database, Command("Карта", 100m));
        await model.LoadAsync();

        Assert.True(Assert.Single(model.Sections).HasSpendable);
    }

    /// <summary>
    /// До первого чтения экран не утверждает ни что счета есть, ни что их нет.
    /// Иначе приглашение завести первый счёт и кнопка «записать операцию» успевали
    /// бы мигнуть на каждом заходе — обе привязаны к этим же признакам.
    /// </summary>
    [Fact]
    public async Task До_чтения_экран_не_говорит_ни_о_счетах_ни_об_их_отсутствии()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        BalancesViewModel model = Model(database);

        Assert.False(model.IsLoaded);
        Assert.False(model.HasAccounts);
        Assert.False(model.IsEmpty);

        await model.LoadAsync();

        Assert.True(model.IsLoaded);
        Assert.True(model.IsEmpty);
        Assert.False(model.HasAccounts);
    }

    /// <summary>
    /// Пока счета в плюсе, ни строка, ни подытог смысловым цветом не красятся.
    /// </summary>
    [Fact]
    public async Task Положительный_баланс_не_помечен()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        Guid cash = await SaveAsync(database, Command("Наличные", 1000m));

        BalancesViewModel model = Model(database);
        await model.LoadAsync();

        CurrencySection section = Assert.Single(model.Sections);

        Assert.False(section.IsAvailableNegative);
        Assert.False(Tile(section, cash).IsNegative);
    }

    /// <summary>
    /// Жест обновления и перечитывание по чужой правке совпали, и более раннее
    /// чтение закончилось последним. Его устаревшие счета не должны перекрыть свежие.
    /// </summary>
    [Fact]
    public async Task Отставшее_чтение_не_перекрывает_свежее()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await SaveAsync(database, Command("Наличные", 1000m));

        DelayingAccounts accounts = new(database.Resolve<IAccountsQuery>()) { Delay = new TaskCompletionSource() };
        BalancesViewModel model = new(accounts, database.Resolve<IChangeNotifier>());

        Task slow = model.LoadAsync();

        TaskCompletionSource hold = accounts.Delay;
        accounts.Delay = null;
        await SaveAsync(database, Command("Карта", 500m));
        await model.LoadAsync();

        hold.SetResult();
        await slow;

        Assert.Equal(2, Assert.Single(model.Sections).Spendable.Count);
    }

    /// <summary>
    /// Читает счета сразу, а отдаёт по сигналу теста: так прочитанное оказывается
    /// старше того, что успело записаться, пока чтение ждало.
    /// </summary>
    private sealed class DelayingAccounts(IAccountsQuery inner) : IAccountsQuery
    {
        public TaskCompletionSource? Delay { get; set; }

        public async Task<IReadOnlyList<AccountListItem>> ReadAsync(CancellationToken cancellationToken = default)
        {
            TaskCompletionSource? delay = Delay;
            IReadOnlyList<AccountListItem> accounts = await inner.ReadAsync(cancellationToken);

            if (delay is not null)
            {
                await delay.Task;
            }

            return accounts;
        }

        public Task<AccountListItem?> ReadOneAsync(Guid key, CancellationToken cancellationToken = default) =>
            inner.ReadOneAsync(key, cancellationToken);
    }

    private static BalancesViewModel Model(TestDatabase database) =>
        database.Resolve<BalancesViewModel>();

    private static AccountTile Tile(CurrencySection section, Guid key) =>
        section.Spendable.Concat(section.Savings).Single(tile => tile.Key == key);
}

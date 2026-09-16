using Finance.Application.Features.Transactions.Card;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Tests;

/// <summary>
/// Клавиатура суммы в форме операции: куда уходят нажатия и когда загорается
/// сохранение. Клавиатура одна, а полей суммы у перевода между валютами два.
/// </summary>
public sealed class TransactionKeypadTests
{
    /// <summary>Набранное клавишами становится суммой, а итог выражения показан рядом.</summary>
    [Fact]
    public async Task Набранное_клавишами_становится_суммой()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        foreach (char key in "1250+340")
        {
            model.PressKeyCommand.Execute(key.ToString());
        }

        Assert.Equal("1250+340", model.Amount);
        // Разряды в показе разделены неразрывным пробелом — сверяется хвост суммы
        Assert.Contains("590,00", model.AmountPreview, StringComparison.Ordinal);
        Assert.True(model.CanSave);
    }

    /// <summary>
    /// Пустая и нулевая сумма сохранение не включают: кнопка гаснет, а не
    /// отказывает после нажатия нарушением <c>Invariant.AmountIsPositive</c>.
    /// </summary>
    [Fact]
    public async Task Нулевая_сумма_не_включает_сохранение()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        Assert.False(model.CanSave);

        // Пока ничего не набрано, выражение пусто, а итог показывает ноль в валюте счёта
        Assert.Equal(string.Empty, model.AmountDisplay);
        Assert.Equal(Money.Restore(0m, Currency.RUB).Display, model.AmountPreview);

        model.PressKeyCommand.Execute("0");

        Assert.False(model.CanSave);

        model.PressKeyCommand.Execute("5");

        Assert.True(model.CanSave);
    }

    /// <summary>
    /// У перевода между валютами нажатия уходят в то поле, которое выбрано.
    /// Обе суммы обязаны быть набраны — иначе сохранять нечего.
    /// </summary>
    [Fact]
    public async Task Нажатия_уходят_в_выбранное_поле()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid rubles = await fixture.AccountAsync("Карта");
        Guid euros = await fixture.AccountAsync("Валютный", currency: Currency.EUR);

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        model.Kind = TransactionKind.Transfer;
        model.SourceAccount = model.Accounts.First(account => account.Key == rubles);
        model.TargetAccount = model.Accounts.First(account => account.Key == euros);

        Assert.True(model.NeedsTargetAmount);

        model.PressKeyCommand.Execute("1");
        model.PressKeyCommand.Execute("0");
        model.PressKeyCommand.Execute("0");

        Assert.Equal("100", model.Amount);
        Assert.False(model.CanSave);

        model.ActivateTargetAmountCommand.Execute(null);
        model.PressKeyCommand.Execute("1");

        Assert.True(model.IsTargetAmountActive);
        Assert.Equal("100", model.Amount);
        Assert.Equal("1", model.TargetAmount);
        Assert.True(model.CanSave);

        // Возврат к расходу убирает второе поле, и набор возвращается в первое
        model.Kind = TransactionKind.Expense;

        Assert.True(model.IsSourceAmountActive);

        model.PressKeyCommand.Execute("5");

        Assert.Equal("1005", model.Amount);
    }

    /// <summary>Стирание работает в том же поле, что и набор.</summary>
    [Fact]
    public async Task Стирание_работает_в_выбранном_поле()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        model.PressKeyCommand.Execute("1");
        model.PressKeyCommand.Execute("2");
        model.BackspaceCommand.Execute(null);

        Assert.Equal("1", model.Amount);
    }

    /// <summary>
    /// Сумма правимой операции показывается с запятой: точку клавиатура не
    /// набирает, и стереть её пришлось бы вслепую.
    /// </summary>
    [Fact]
    public async Task Сумма_правимой_операции_показана_с_запятой()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid account = await fixture.AccountAsync("Карта");
        Guid transaction = await fixture.SaveAsync(fixture.Expense(account, 12.34m));

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(transaction);

        Assert.Equal("12,34", model.Amount);
    }

    /// <summary>
    /// Два нажатия «Сохранить» подряд записывают одну операцию: кнопка зовёт
    /// метод напрямую, и без флага занятости второе нажатие до ухода экрана
    /// завело бы вторую запись с новым ключом.
    /// </summary>
    [Fact]
    public async Task Двойное_нажатие_сохраняет_одну_операцию()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        Guid card = await fixture.AccountAsync("Карта");

        // Запись в SQLite завершается на месте, и гонку без задержки не подстроить
        DelayingSave save = new(fixture.Database.Resolve<ISaveTransactionHandler>());
        TransactionViewModel model = Model(fixture, save);
        await model.LoadAsync(key: null);
        model.Category = model.Categories[0];
        model.PressKeyCommand.Execute("5");

        Task<bool> first = model.SaveAsync();
        Task<bool> second = model.SaveAsync();

        save.Delay.SetResult();

        Assert.True(await first);
        Assert.False(await second);
        Assert.Single((await fixture.FeedAsync(card)).Items);
    }

    /// <summary>Держит сохранение, пока тест не отпустит: так второе нажатие приходится на первое.</summary>
    private sealed class DelayingSave(ISaveTransactionHandler inner) : ISaveTransactionHandler
    {
        public TaskCompletionSource Delay { get; } = new();

        public async Task<Guid> HandleAsync(SaveTransactionCommand command, CancellationToken cancellationToken = default)
        {
            await Delay.Task;

            return await inner.HandleAsync(command, cancellationToken);
        }
    }

    private static TransactionViewModel Model(TransactionFixture fixture, ISaveTransactionHandler? save = null) => new(
        fixture.Database.Resolve<ITransactionFormQuery>(),
        fixture.Database.Resolve<ITransactionCardQuery>(),
        save ?? fixture.Database.Resolve<ISaveTransactionHandler>(),
        fixture.Database.Resolve<IDeleteTransactionHandler>(),
        fixture.Database.Resolve<IAccountsQuery>(),
        fixture.Database.Resolve<Finance.Application.Infrastructure.IClock>(),
        fixture.Database.Resolve<TransactionPicks>());
}

using Finance.Application.Features.Transactions.Card;
using Finance.Application.Features.Transactions.Pick;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Клавиатура суммы в форме операции: куда уходят нажатия и когда загорается
/// сохранение. Клавиатура одна, а полей суммы у перевода между валютами два.
/// </summary>
public sealed class TransactionKeypadTests
{
    /// <summary>
    /// Набранное клавишами становится суммой. Итог незакрытого действия не
    /// показывается: его даёт «=», а сохранение и без неё запишет тот же итог.
    /// </summary>
    [Fact]
    public async Task Набранное_клавишами_становится_суммой()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        Press(model, "1250+340");

        Assert.Equal("1250+340", model.Amount);
        Assert.True(model.HasAmountOperation);
        Assert.True(model.CanSave);

        model.EvaluateCommand.Execute(null);

        Assert.Equal("1590", model.Amount);
        // Сверка через Display: разряды разделены пробелом, который в тесте не набрать
        Assert.Equal(Money.Restore(-1590m, Currency.RUB).DisplaySigned, model.AmountHero);
    }

    /// <summary>
    /// «=» сворачивает выражение в число и уходит в то же поле, что и цифры:
    /// у перевода между валютами их два, и соседнее поле остаётся как было.
    /// </summary>
    [Fact]
    public async Task Равно_сворачивает_выражение_в_выбранном_поле()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        TransactionViewModel model = await TransferBetweenCurrenciesAsync(fixture);

        Press(model, "90×2");
        model.ActivateTargetAmountCommand.Execute(null);
        Press(model, "10÷4");

        model.EvaluateCommand.Execute(null);

        Assert.Equal("90×2", model.Amount);
        Assert.Equal("2,5", model.TargetAmount);
    }

    /// <summary>
    /// Итог «=» возвращается в поле теми же знаками, что набирают, — с запятой,
    /// поэтому набор продолжается с него, а не с пустого поля. Проверяется на поле
    /// зачисления: оно второе, и итог обязан вернуться именно в него.
    /// </summary>
    [Fact]
    public async Task Набор_продолжается_с_итога()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        TransactionViewModel model = await TransferBetweenCurrenciesAsync(fixture);

        model.ActivateTargetAmountCommand.Execute(null);
        Press(model, "10÷4");
        model.EvaluateCommand.Execute(null);
        model.PressKeyCommand.Execute("5");

        Assert.Equal("2,55", model.TargetAmount);
        Assert.Equal(string.Empty, model.Amount);
    }

    /// <summary>
    /// Незаконченное выражение «=» не трогает: считать ей нечего.
    /// </summary>
    [Fact]
    public async Task Равно_не_меняет_незаконченное_выражение()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        model.EvaluateCommand.Execute(null);

        Assert.Equal(string.Empty, model.Amount);

        model.PressKeyCommand.Execute("7");
        model.PressKeyCommand.Execute("+");
        model.EvaluateCommand.Execute(null);

        Assert.Equal("7+", model.Amount);
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
        Assert.Equal(Money.Restore(0m, Currency.RUB).Display, model.AmountHero);

        model.PressKeyCommand.Execute("0");

        Assert.False(model.CanSave);

        model.PressKeyCommand.Execute("5");

        Assert.True(model.CanSave);
    }

    /// <summary>
    /// Итог крупно подписан знаком вида: расход минусом, доход плюсом, перевод
    /// без знака. Ноль знака не получает — «−0,00 ₽» читался бы как долг.
    /// </summary>
    [Fact]
    public async Task Итог_подписан_знаком_вида()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        Assert.Equal(Money.Restore(0m, Currency.RUB).Display, model.AmountHero);
        Assert.Equal(AmountTone.Placeholder, model.AmountTone);

        model.PressKeyCommand.Execute("1");
        model.PressKeyCommand.Execute("2");

        Assert.Equal(Money.Restore(-12m, Currency.RUB).DisplaySigned, model.AmountHero);
        Assert.StartsWith("-", model.AmountHero, StringComparison.Ordinal);
        Assert.Equal(AmountTone.Expense, model.AmountTone);

        model.Kind = TransactionKind.Income;

        Assert.StartsWith("+", model.AmountHero, StringComparison.Ordinal);
        Assert.Equal(AmountTone.Income, model.AmountTone);

        model.Kind = TransactionKind.Transfer;

        Assert.Equal(Money.Restore(12m, Currency.RUB).Display, model.AmountHero);
        Assert.Equal(AmountTone.Plain, model.AmountTone);
    }

    /// <summary>
    /// Выражение над итогом показано только со знаком действия: у простого числа
    /// оно повторяло бы итог. Пока действие не закрыто, вместо итога подсказка
    /// про «=», а знак вида не ставится — перед выражением он читался бы вычитанием.
    /// </summary>
    [Fact]
    public async Task Выражение_показано_только_со_знаком_действия()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        model.PressKeyCommand.Execute("7");

        Assert.False(model.HasAmountOperation);

        model.PressKeyCommand.Execute("+");
        model.PressKeyCommand.Execute("5");

        Assert.True(model.HasAmountOperation);
        Assert.Equal(UiTexts.TransactionPressEquals, model.AmountHero);

        model.EvaluateCommand.Execute(null);

        Assert.False(model.HasAmountOperation);
        Assert.Equal(Money.Restore(-12m, Currency.RUB).DisplaySigned, model.AmountHero);
    }

    /// <summary>
    /// У перевода между валютами нажатия уходят в то поле, которое выбрано.
    /// </summary>
    [Fact]
    public async Task Нажатия_уходят_в_выбранное_поле()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        TransactionViewModel model = await TransferBetweenCurrenciesAsync(fixture);

        Press(model, "100");
        model.ActivateTargetAmountCommand.Execute(null);
        Press(model, "1");

        Assert.True(model.IsTargetAmountActive);
        Assert.Equal("100", model.Amount);
        Assert.Equal("1", model.TargetAmount);
    }

    /// <summary>
    /// Перевод между валютами сохраняется только с обеими суммами: курс
    /// приложение не знает, и вторую сумму взять неоткуда.
    /// </summary>
    [Fact]
    public async Task Перевод_между_валютами_сохраняется_с_обеими_суммами()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        TransactionViewModel model = await TransferBetweenCurrenciesAsync(fixture);

        Assert.True(model.NeedsTargetAmount);

        Press(model, "100");

        Assert.False(model.CanSave);

        model.ActivateTargetAmountCommand.Execute(null);
        Press(model, "1");

        Assert.True(model.CanSave);
    }

    /// <summary>
    /// Возврат к расходу убирает второе поле, и набор возвращается в первое,
    /// а не уходит в невидимое.
    /// </summary>
    [Fact]
    public async Task Смена_вида_возвращает_набор_в_первое_поле()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        TransactionViewModel model = await TransferBetweenCurrenciesAsync(fixture);

        Press(model, "100");
        model.ActivateTargetAmountCommand.Execute(null);

        model.Kind = TransactionKind.Expense;
        model.PressKeyCommand.Execute("5");

        Assert.True(model.IsSourceAmountActive);
        Assert.Equal("1005", model.Amount);
    }

    /// <summary>
    /// Стирание работает в том же поле, что и набор.
    /// </summary>
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

    /// <summary>
    /// Держит сохранение, пока тест не отпустит: так второе нажатие приходится на первое.
    /// </summary>
    private sealed class DelayingSave(ISaveTransactionHandler inner) : ISaveTransactionHandler
    {
        public TaskCompletionSource Delay { get; } = new();

        public async Task<Guid> HandleAsync(SaveTransactionCommand command, CancellationToken cancellationToken = default)
        {
            await Delay.Task;

            return await inner.HandleAsync(command, cancellationToken);
        }
    }

    /// <summary>
    /// Форма перевода с рублёвого счёта на евровый: у неё два поля суммы.
    /// </summary>
    private static async Task<TransactionViewModel> TransferBetweenCurrenciesAsync(TransactionFixture fixture)
    {
        Guid rubles = await fixture.AccountAsync("Карта");
        Guid euros = await fixture.AccountAsync("Валютный", currency: Currency.EUR);

        TransactionViewModel model = Model(fixture);
        await model.LoadAsync(key: null);

        model.Kind = TransactionKind.Transfer;
        model.SourceAccount = model.Accounts.First(account => account.Key == rubles);
        model.TargetAccount = model.Accounts.First(account => account.Key == euros);

        return model;
    }

    private static void Press(TransactionViewModel model, string keys)
    {
        foreach (char key in keys)
        {
            model.PressKeyCommand.Execute(key.ToString());
        }
    }

    private static TransactionViewModel Model(TransactionFixture fixture, ISaveTransactionHandler? save = null) => new(
        fixture.Database.Resolve<ITransactionFormQuery>(),
        fixture.Database.Resolve<ITransactionCardQuery>(),
        save ?? fixture.Database.Resolve<ISaveTransactionHandler>(),
        fixture.Database.Resolve<IDeleteTransactionHandler>(),
        fixture.Database.Resolve<IAccountsQuery>(),
        fixture.Database.Resolve<IClock>(),
        fixture.Database.Resolve<TransactionPicks>());
}

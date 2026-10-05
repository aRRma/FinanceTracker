using Finance.Application.Features.Accounts.Card;
using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Deletion;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;
using static Finance.Application.Tests.TypedAmount;

namespace Finance.Application.Tests;

/// <summary>
/// Карточка счёта: остаток с клавиатуры суммы, подсказка названия по типу и удаление.
/// </summary>
public sealed class AccountCardTests
{
    /// <summary>
    /// Ведущие нули заменяются, а запятая — та, что на клавиатуре: «0007» — это 7.
    /// </summary>
    [Fact]
    public async Task Остаток_набирается_клавиатурой_суммы()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = await NewAsync(fixture, "Kopilka");

        Press(model, "0007");
        Assert.Equal("7", model.OpeningBalance);

        model.Backspace();
        Press(model, "1,5");

        Assert.Equal("1,5", model.OpeningBalance);
        Assert.Equal(Money.Restore(1.5m, Currency.RUB).Display, model.OpeningBalanceDisplay);
        Assert.Equal(1.5m, await SavedBalanceAsync(fixture, model));
    }

    /// <summary>
    /// Минус первой клавишей — долг: «−», «15000» дают −15 000, а сумма
    /// показывается цветом расхода.
    /// </summary>
    [Fact]
    public async Task Минус_первым_даёт_отрицательный_остаток()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = await NewAsync(fixture, "Kredit");

        Press(model, "−15000");

        Assert.True(model.IsOpeningBalanceNegative);
        Assert.Equal(Whole(Money.Restore(-15_000m, Currency.RUB).Display), model.OpeningBalanceDisplay);
        Assert.Equal(-15_000m, await SavedBalanceAsync(fixture, model));
    }

    /// <summary>
    /// Выражение показывается как набрано, пока его не свернёт «=»; сохранить
    /// можно и не сворачивая — записывается тот же итог.
    /// </summary>
    [Fact]
    public async Task Выражение_в_остатке_сворачивается_по_равно()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = await NewAsync(fixture, "Konvert");

        Press(model, "1000+250");
        Assert.Equal("1000+250", model.OpeningBalanceDisplay);

        model.Evaluate();
        Assert.Equal("1250", model.OpeningBalance);

        Press(model, "−50");
        Assert.Equal(1_200m, await SavedBalanceAsync(fixture, model));
    }

    /// <summary>
    /// Пустой остаток — ноль, а не недобранный ввод.
    /// </summary>
    [Fact]
    public async Task Пустой_остаток_сохраняется_нулём()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = await NewAsync(fixture, "Pustoy");

        Assert.Equal(Whole(Money.Restore(0m, Currency.RUB).Display), model.OpeningBalanceDisplay);
        Assert.Equal(0m, await SavedBalanceAsync(fixture, model));
    }

    /// <summary>
    /// Записанный остаток встаёт в поле запятой культуры, а не точкой, и правится
    /// с конца теми же клавишами.
    /// </summary>
    [Fact]
    public async Task Записанный_остаток_правится_клавиатурой()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid key = await fixture.AccountAsync("Karta", 1234.5m);
        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);

        Assert.Equal("1234,5", model.OpeningBalance);
        Assert.False(model.IsDirty);

        model.Backspace();
        Press(model, "7");

        Assert.Equal("1234,7", model.OpeningBalance);
    }

    /// <summary>
    /// Нулевой остаток встаёт пустым полем: минус с него начинает долг, а не вычитание.
    /// </summary>
    [Fact]
    public async Task Нулевой_остаток_встаёт_пустым_полем()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid key = await fixture.AccountAsync("Nol");
        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);

        Press(model, "−5");

        Assert.Equal("−5", model.OpeningBalance);
        Assert.True(model.IsOpeningBalanceNegative);
    }

    /// <summary>
    /// Подсказка названия своя у каждого типа и меняется вместе с ним.
    /// </summary>
    [Fact]
    public async Task Подсказка_названия_меняется_с_типом()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key: null);

        Assert.Equal(UiTexts.AccountNamePlaceholderCard, model.NamePlaceholder);

        List<string?> changed = [];
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        model.TypeIndex = 1;

        Assert.Equal(AccountType.Cash, model.Type);
        Assert.Equal(UiTexts.AccountNamePlaceholderCash, model.NamePlaceholder);
        Assert.Contains(nameof(AccountViewModel.NamePlaceholder), changed);
    }

    /// <summary>
    /// Клавиатура суммы появляется только по касанию остатка: при открытии формы
    /// набирают название, и две клавиатуры на экран не помещаются.
    /// </summary>
    [Fact]
    public async Task Клавиатура_появляется_по_касанию_остатка()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = await NewAsync(fixture, "Klaviatura");

        Assert.False(model.AreKeysVisible);

        model.ShowKeys();

        Assert.True(model.AreKeysVisible);
    }

    /// <summary>
    /// Пока клавиатура на виду, остаток показан как набран: сумма с валютой спрятала
    /// бы запятую, и «1,» выглядело бы непринятым нажатием. Без клавиатуры — суммой.
    /// </summary>
    [Fact]
    public async Task Пока_клавиатура_на_виду_остаток_показан_как_набран()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = await NewAsync(fixture, "Zapyataya");

        model.ShowKeys();
        Press(model, "1,");

        Assert.Equal("1,", model.OpeningBalanceDisplay);

        model.AreKeysVisible = false;

        // Набор окончен, а запятая так и висит без цифр: это то же целое число
        Assert.Equal(Whole(Money.Restore(1m, Currency.RUB).Display), model.OpeningBalanceDisplay);
    }

    /// <summary>
    /// Правкой остатка считается другое число, а не другая запись того же: «0» на
    /// пустом поле и ноль после запятой уход с формы не останавливают. Недобранный
    /// ввод — правка: его потеря незаметной не будет.
    /// </summary>
    [Fact]
    public async Task Правка_остатка_сверяется_по_значению()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid key = await fixture.AccountAsync("Kopeyki", 1234.5m);
        AccountViewModel saved = fixture.Database.Resolve<AccountViewModel>();
        await saved.LoadAsync(key);

        Press(saved, "0");

        Assert.Equal("1234,50", saved.OpeningBalance);
        Assert.False(saved.IsDirty);

        AccountViewModel fresh = fixture.Database.Resolve<AccountViewModel>();
        await fresh.LoadAsync(key: null);

        Press(fresh, "0");
        Assert.False(fresh.IsDirty);

        fresh.Backspace();
        Press(fresh, "−");
        Assert.True(fresh.IsDirty);
    }

    /// <summary>
    /// Недобранный остаток не сохраняется: одинокий минус или знак действия в конце —
    /// карточка правила, а не ноль и не падение; сохранить исправленное можно снова.
    /// </summary>
    [Theory]
    [InlineData("−")]
    [InlineData("100+")]
    public async Task Недобранный_остаток_не_сохраняется(string typed)
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        AccountViewModel model = await NewAsync(fixture, "Nedobor");

        Press(model, typed);

        Assert.False(await model.SaveAsync());
        Assert.Equal(UiTexts.AccountOpeningIncomplete, model.Error);
        Assert.True(model.CanSave);
    }

    /// <summary>
    /// Пустой счёт удаляется и пропадает из списков, а у нового удалять нечего.
    /// </summary>
    [Fact]
    [Trait("Инвариант", nameof(Invariant.AccountDeletedOnlyWithoutTransactions))]
    public async Task Пустой_счёт_удаляется()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid key = await fixture.AccountAsync("Oshibka");
        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();

        await model.LoadAsync(key: null);
        Assert.False(model.IsExisting);
        Assert.False(await model.DeleteAsync());

        await model.LoadAsync(key);

        Assert.True(model.IsExisting);
        Assert.Null(model.DeleteRefusal);
        Assert.Equal(string.Format(UiCulture.Current, UiTexts.AccountDeleteConfirmTitle, "Oshibka"), model.DeleteTitle);
        Assert.True(await model.DeleteAsync(), model.Error);
        Assert.DoesNotContain(await AccountsAsync(fixture), account => account.Key == key);
    }

    /// <summary>
    /// Счёт с операцией не удаляется — ни своей, ни переводом на него: карточка
    /// говорит об этом до подтверждения, а домен отказывает и в обход неё.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Инвариант", nameof(Invariant.AccountDeletedOnlyWithoutTransactions))]
    public async Task Счёт_с_операциями_не_удаляется(bool asTransferTarget)
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid key = await fixture.AccountAsync("Rabochaya");
        Guid other = await fixture.AccountAsync("Drugaya", 1_000m);

        await fixture.SaveAsync(asTransferTarget ? fixture.Transfer(other, key, 100m) : fixture.Expense(key, 100m));

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);

        Assert.Equal(UiTexts.AccountDeleteHasTransactions, model.DeleteRefusal);
        Assert.False(await model.DeleteAsync());
        Assert.True(model.HasError);
        Assert.False(model.IsSaving);
        Assert.Contains(await AccountsAsync(fixture), account => account.Key == key);

        DomainException error = await Assert.ThrowsAsync<DomainException>(
            () => fixture.Database.Resolve<IDeleteAccountHandler>().HandleAsync(key));

        Assert.Equal(Invariant.AccountDeletedOnlyWithoutTransactions, error.Invariant);
    }

    /// <summary>
    /// Удалённые операции удалению не мешают: их не показывает ни один экран.
    /// Счёт, на котором записали последнюю операцию, форма после удаления
    /// не подставляет.
    /// </summary>
    [Fact]
    [Trait("Инвариант", nameof(Invariant.AccountDeletedOnlyWithoutTransactions))]
    public async Task Счёт_только_с_удалёнными_операциями_удаляется()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid kept = await fixture.AccountAsync("Ostavlennaya");
        Guid key = await fixture.AccountAsync("Lishnyaya");

        Guid transaction = await fixture.SaveAsync(fixture.Expense(key, 100m));
        await fixture.Database.Resolve<IDeleteTransactionsHandler>().HandleAsync([transaction]);

        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key);

        Assert.Null(model.DeleteRefusal);
        Assert.True(await model.DeleteAsync(), model.Error);

        TransactionViewModel form = fixture.Database.Resolve<TransactionViewModel>();
        await form.LoadAsync(key: null);

        Assert.NotEqual(key, form.SourceAccount?.Key);
        Assert.Equal(kept, form.SourceAccount?.Key);
    }

    /// <summary>
    /// Повторное удаление — нажатие по экрану, который не успел обновиться, а не ошибка.
    /// </summary>
    [Fact]
    public async Task Повторное_удаление_счёта_безвредно()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid key = await fixture.AccountAsync("Dvazhdy");
        IDeleteAccountHandler handler = fixture.Database.Resolve<IDeleteAccountHandler>();

        await handler.HandleAsync(key);
        await handler.HandleAsync(key);

        Assert.DoesNotContain(await AccountsAsync(fixture), account => account.Key == key);
    }

    private static async Task<AccountViewModel> NewAsync(TransactionFixture fixture, string name)
    {
        AccountViewModel model = fixture.Database.Resolve<AccountViewModel>();
        await model.LoadAsync(key: null);
        model.Name = name;

        return model;
    }

    private static void Press(AccountViewModel model, string keys)
    {
        foreach (char key in keys)
        {
            model.PressKey(key);
        }
    }

    private static async Task<decimal> SavedBalanceAsync(TransactionFixture fixture, AccountViewModel model)
    {
        Assert.True(await model.SaveAsync(), model.Error);

        IReadOnlyList<AccountListItem> accounts = await AccountsAsync(fixture);

        return accounts.Single(account => account.Name == model.Name).Balance.Amount;
    }

    private static Task<IReadOnlyList<AccountListItem>> AccountsAsync(TransactionFixture fixture) =>
        fixture.Database.Resolve<IAccountsQuery>().ReadAsync();
}

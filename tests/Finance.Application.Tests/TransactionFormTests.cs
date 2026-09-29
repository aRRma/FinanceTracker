using Finance.Application.Features.Transactions.Card;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Форма операции целиком: чего не хватает для сохранения, что показывается при
/// нарушенном правиле, удаление с его последствиями и быстрый выбор даты.
/// Клавиатуру суммы проверяет <see cref="TransactionKeypadTests"/>.
/// </summary>
public sealed class TransactionFormTests
{
    /// <summary>
    /// Без счёта сохранять не на что: форма называет, чего не хватает, и ничего не пишет.
    /// </summary>
    [Fact]
    public async Task Без_счёта_называет_причину()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        TransactionViewModel model = await NewAsync(fixture);
        model.Amount = "100";

        await AssertRefusedAsync(fixture, model, UiTexts.TransactionChooseAccount);
    }

    /// <summary>
    /// Пустая сумма не сохраняется, даже если кнопку нажали в обход её погашенного вида.
    /// </summary>
    [Fact]
    public async Task Без_суммы_называет_причину()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = await NewAsync(fixture);
        model.Category = Option(model, fixture.ExpenseCategory);

        await AssertRefusedAsync(fixture, model, UiTexts.TransactionAmountIncomplete);
    }

    /// <summary>
    /// Расход без подкатегории не сохраняется: в отчёте ему негде оказаться.
    /// </summary>
    [Fact]
    public async Task Без_категории_называет_причину()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = await NewAsync(fixture);
        model.Amount = "100";

        await AssertRefusedAsync(fixture, model, UiTexts.TransactionChooseCategory);
    }

    /// <summary>
    /// Перевод без счёта зачисления не сохраняется.
    /// </summary>
    [Fact]
    public async Task Перевод_без_счёта_зачисления_называет_причину()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = await NewAsync(fixture);
        model.Kind = TransactionKind.Transfer;
        model.Amount = "100";

        await AssertRefusedAsync(fixture, model, UiTexts.TransactionChooseTargetAccount);
    }

    /// <summary>
    /// Перевод между валютами без второй суммы не сохраняется: курс приложению
    /// неизвестен, и зачисленную сумму взять неоткуда.
    /// </summary>
    [Fact]
    public async Task Перевод_между_валютами_без_второй_суммы_называет_причину()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");
        Guid euros = await fixture.AccountAsync("Валютный", currency: Currency.EUR);

        TransactionViewModel model = await NewAsync(fixture);
        model.Kind = TransactionKind.Transfer;
        model.TargetAccount = model.TargetAccounts.Single(option => option.Key == euros);
        model.Amount = "100";

        await AssertRefusedAsync(fixture, model, UiTexts.TransactionTargetAmountIncomplete);
    }

    /// <summary>
    /// Нарушенное доменное правило — ввод пользователя, а не сбой: форма показывает
    /// его текст и снимает признак сохранения, чтобы исправленное сохранилось.
    /// </summary>
    [Fact]
    public async Task Нарушенное_правило_показывается_и_исправляется()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = await FilledExpenseAsync(fixture, amount: "100");
        model.OccurredOn = TransactionFixture.OpenedOn.AddDays(-1);

        Assert.False(await model.SaveAsync());
        Assert.True(model.HasError);
        Assert.False(model.IsSaving);

        model.SetToday();

        Assert.True(await model.SaveAsync(), model.Error);
        Assert.False(model.HasError);
    }

    /// <summary>
    /// Заполненная форма записывает операцию со всеми полями. После удачи признак
    /// сохранения остаётся: экран закрывается, и второе нажатие в этот промежуток
    /// записало бы операцию дважды.
    /// </summary>
    [Fact]
    public async Task Сохранённая_форма_записывает_операцию_один_раз()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = await FilledExpenseAsync(fixture, amount: "250");
        model.PlaceName = "Пятёрочка";
        model.Note = "к ужину";

        Assert.True(await model.SaveAsync(), model.Error);
        Assert.True(model.IsSaving);
        Assert.False(await model.SaveAsync());

        FeedItem item = Assert.Single((await fixture.FeedAsync()).Items);

        // Лента показывает сумму со стороны счёта: у расхода она со знаком минус
        Assert.Equal(TransactionKind.Expense, item.Kind);
        Assert.Equal(Money.Create(-250m, Currency.RUB), item.Amount);
        Assert.Equal("Пятёрочка", item.Place);
        Assert.Equal("к ужину", item.Note);
        Assert.Equal(fixture.Today, item.OccurredOn);
    }

    /// <summary>
    /// Подтверждение удаления называет, каким станет баланс каждого счёта операции:
    /// иначе пользователь подтверждает вслепую.
    /// </summary>
    [Fact]
    public async Task Подтверждение_удаления_называет_балансы_после()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid card = await fixture.AccountAsync("Карта", 1000m);
        Guid cash = await fixture.AccountAsync("Наличные", 0m);
        Guid transfer = await fixture.SaveAsync(fixture.Transfer(card, cash, 300m));

        TransactionViewModel model = fixture.Database.Resolve<TransactionViewModel>();
        await model.LoadAsync(transfer);

        string prompt = await model.DeletePromptAsync();

        // Сейчас на карте 700, в наличных 300: удаление вернёт 1000 и 0
        Assert.Contains(BalanceAfter("Карта", 1000m), prompt, StringComparison.Ordinal);
        Assert.Contains(BalanceAfter("Наличные", 0m), prompt, StringComparison.Ordinal);
        Assert.EndsWith(UiTexts.TransactionDeleteIrreversible, prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// У несохранённой операции последствий нет — только предупреждение о необратимости.
    /// </summary>
    [Fact]
    public async Task Подтверждение_удаления_новой_операции_без_балансов()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = await NewAsync(fixture);

        Assert.Equal(UiTexts.TransactionDeleteIrreversible, await model.DeletePromptAsync());
        Assert.False(await model.DeleteAsync());
    }

    /// <summary>
    /// Удаление убирает операцию из ленты и держит признак, как сохранение:
    /// второе нажатие до закрытия экрана ничего не делает.
    /// </summary>
    [Fact]
    public async Task Удаление_убирает_операцию_один_раз()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid card = await fixture.AccountAsync("Карта");
        Guid expense = await fixture.SaveAsync(fixture.Expense(card, 100m));

        TransactionViewModel model = fixture.Database.Resolve<TransactionViewModel>();
        await model.LoadAsync(expense);

        Assert.Equal(UiTexts.TransactionTitleExisting, model.Title);
        Assert.True(await model.DeleteAsync());
        Assert.False(await model.DeleteAsync());
        Assert.Empty((await fixture.FeedAsync()).Items);
    }

    /// <summary>
    /// Быстрый выбор ставит сегодня и вчера по часам приложения, подпись называет их
    /// словами, а остальные даты — числом. Календарь не пускает раньше открытия
    /// счёта и позже сегодняшнего дня.
    /// </summary>
    [Fact]
    public async Task Дата_выбирается_в_пределах_счёта_и_сегодня()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();
        await fixture.AccountAsync("Карта");

        TransactionViewModel model = await NewAsync(fixture);

        model.SetYesterday();

        Assert.True(model.IsYesterday);
        Assert.Equal(UiTexts.TransactionYesterday, model.OccurredOnCaption);

        model.SetToday();

        Assert.True(model.IsToday);
        Assert.Equal(UiTexts.TransactionToday, model.OccurredOnCaption);

        DateOnly earlier = fixture.Today.AddDays(-10);
        model.OccurredOnDate = earlier.ToDateTime(TimeOnly.MinValue);

        Assert.Equal(earlier, model.OccurredOn);
        Assert.Equal(DateText.DayWithYearIfOther(earlier, fixture.Today), model.OccurredOnCaption);

        Assert.Equal(TransactionFixture.OpenedOn.ToDateTime(TimeOnly.MinValue), model.EarliestDate);
        Assert.Equal(fixture.Today.ToDateTime(TimeOnly.MinValue), model.LatestDate);
    }

    /// <summary>
    /// Счёт, ставший счётом списания, уходит из выбора зачисления, а если он там
    /// уже стоял — сбрасывается: перевод со счёта на него же не имеет смысла.
    /// </summary>
    [Fact]
    public async Task Счёт_списания_уходит_из_выбора_зачисления()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid card = await fixture.AccountAsync("Карта");
        Guid cash = await fixture.AccountAsync("Наличные");

        TransactionViewModel model = await NewAsync(fixture);
        model.Kind = TransactionKind.Transfer;
        model.SourceAccount = model.Accounts.Single(option => option.Key == card);
        model.TargetAccount = model.TargetAccounts.Single(option => option.Key == cash);

        model.SourceAccount = model.Accounts.Single(option => option.Key == cash);

        Assert.Equal(TransactionKind.Transfer, model.Kind);
        Assert.Null(model.TargetAccount);
        Assert.Equal(UiTexts.CommonChoose, model.TargetAccountCaption);
        Assert.DoesNotContain(model.TargetAccounts, option => option.Key == cash);
    }

    /// <summary>
    /// Дата раньше открытия нового счёта переносится на день открытия, и форма
    /// говорит об этом: молча операция ушла бы другим днём. Счёт, открытый раньше
    /// даты, её не трогает и ничего не сообщает.
    /// </summary>
    [Fact]
    public async Task Смена_счёта_переносит_дату_на_открытие_и_сообщает()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid card = await fixture.AccountAsync("Карта");
        DateOnly opened = fixture.Today.AddDays(-5);
        Guid deposit = await fixture.AccountAsync("Вклад", openedOn: opened);

        TransactionViewModel model = await NewAsync(fixture);
        List<string> notices = [];
        model.Notified += notices.Add;

        model.SourceAccount = model.Accounts.Single(option => option.Key == card);
        model.OccurredOnDate = fixture.Today.AddDays(-14).ToDateTime(TimeOnly.MinValue);

        model.SourceAccount = model.Accounts.Single(option => option.Key == deposit);

        Assert.Equal(opened, model.OccurredOn);
        Assert.Equal(
            [string.Format(UiCulture.Current, UiTexts.TransactionDateMoved, DateText.DayWithYearIfOther(opened, fixture.Today))],
            notices);

        model.SourceAccount = model.Accounts.Single(option => option.Key == card);

        Assert.Equal(opened, model.OccurredOn);
        Assert.Single(notices);
    }

    /// <summary>
    /// У перевода дату ограничивает и счёт зачисления: выбор его тоже переносит дату и сообщает.
    /// </summary>
    [Fact]
    public async Task Счёт_зачисления_переносит_дату_перевода()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid card = await fixture.AccountAsync("Карта");
        DateOnly opened = fixture.Today.AddDays(-5);
        Guid deposit = await fixture.AccountAsync("Вклад", openedOn: opened);

        TransactionViewModel model = await NewAsync(fixture);
        List<string> notices = [];
        model.Notified += notices.Add;

        model.Kind = TransactionKind.Transfer;
        model.SourceAccount = model.Accounts.Single(option => option.Key == card);
        model.OccurredOnDate = fixture.Today.AddDays(-14).ToDateTime(TimeOnly.MinValue);

        model.TargetAccount = model.TargetAccounts.Single(option => option.Key == deposit);

        Assert.Equal(opened, model.OccurredOn);
        Assert.Equal(
            [string.Format(UiCulture.Current, UiTexts.TransactionDateMoved, DateText.DayWithYearIfOther(opened, fixture.Today))],
            notices);
    }

    /// <summary>
    /// Смена счёта на счёт в другой валюте сообщается: набранное число осталось,
    /// а деньги уже другие. Счёт в той же валюте ничего не сообщает.
    /// </summary>
    [Fact]
    public async Task Смена_валюты_счёта_сообщается()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid card = await fixture.AccountAsync("Карта");
        Guid cash = await fixture.AccountAsync("Наличные");
        Guid euros = await fixture.AccountAsync("Валютный", currency: Currency.EUR);

        TransactionViewModel model = await NewAsync(fixture);
        model.SourceAccount = model.Accounts.Single(option => option.Key == card);
        List<string> notices = [];
        model.Notified += notices.Add;

        model.SourceAccount = model.Accounts.Single(option => option.Key == cash);

        Assert.Empty(notices);

        model.SourceAccount = model.Accounts.Single(option => option.Key == euros);

        Assert.Equal([UiTexts.TransactionCurrencyChanged], notices);
    }

    /// <summary>
    /// Открытие формы ничего не сообщает: и правка записанной операции, и новая
    /// операция со счётом в другой валюте. Сообщения — о том, что поменял
    /// пользователь, а при загрузке он ещё ничего не трогал.
    /// </summary>
    [Fact]
    public async Task Открытие_формы_ничего_не_сообщает()
    {
        await using TransactionFixture fixture = await TransactionFixture.CreateAsync();

        Guid card = await fixture.AccountAsync("Карта", openedOn: fixture.Today.AddDays(-5));
        Guid euros = await fixture.AccountAsync("Валютный", currency: Currency.EUR);
        Guid expense = await fixture.SaveAsync(fixture.Expense(card, 100m));

        List<string> notices = [];

        TransactionViewModel existing = fixture.Database.Resolve<TransactionViewModel>();
        existing.Notified += notices.Add;
        await existing.LoadAsync(expense);

        TransactionViewModel fromFeed = fixture.Database.Resolve<TransactionViewModel>();
        fromFeed.Notified += notices.Add;
        await fromFeed.LoadAsync(key: null, accountKey: euros);

        Assert.Empty(notices);
        Assert.Equal(euros, fromFeed.SourceAccount?.Key);
    }

    private static async Task AssertRefusedAsync(TransactionFixture fixture, TransactionViewModel model, string reason)
    {
        Assert.False(await model.SaveAsync());
        Assert.Equal(reason, model.Error);
        Assert.False(model.IsSaving);
        Assert.Empty((await fixture.FeedAsync()).Items);
    }

    private static async Task<TransactionViewModel> NewAsync(TransactionFixture fixture)
    {
        TransactionViewModel model = fixture.Database.Resolve<TransactionViewModel>();
        await model.LoadAsync(key: null);

        Assert.Equal(UiTexts.TransactionTitleNew, model.Title);

        return model;
    }

    private static async Task<TransactionViewModel> FilledExpenseAsync(TransactionFixture fixture, string amount)
    {
        TransactionViewModel model = await NewAsync(fixture);
        model.Amount = amount;
        model.Category = Option(model, fixture.ExpenseCategory);

        return model;
    }

    private static CategoryOption Option(TransactionViewModel model, Guid category) =>
        model.Categories.Single(option => option.Key == category);

    private static string BalanceAfter(string account, decimal amount) =>
        string.Format(UiCulture.Current, UiTexts.TransactionBalanceAfter, account, Money.Create(amount, Currency.RUB).Display);
}

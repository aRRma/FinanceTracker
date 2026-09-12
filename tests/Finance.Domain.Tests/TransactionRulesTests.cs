namespace Finance.Domain.Tests;

/// <summary>Проверки операции против её счетов и категории.</summary>
public sealed class TransactionRulesTests
{
    private static readonly Category Food = Given.Group("Еда");
    private static readonly Category Groceries = Given.Subcategory(Food);
    private static readonly Category Salary = Given.Group("Доходы", CategoryKind.Income);
    private static readonly Category Wage = Given.Subcategory(Salary, "Зарплата");

    [Fact]
    public void Сумма_в_чужой_валюте_отвергается()
    {
        Account dollars = Given.Account("Валютный", Currency.USD);
        // Операция создана корректно сама по себе: валюта расходится только со счётом.
        // Пользователь так ошибиться не может — валюту подставляет счёт, — поэтому
        // это ошибка вызывающего кода, а не нарушение инварианта
        Transaction expense = Transaction.Create(
            TransactionKind.Expense, dollars.Key, Given.Rubles(100m), null, null, Groceries.Key, null,
            Given.Today, null, Given.Today, Given.NowUtc);

        Assert.Throws<ArgumentException>(
            () => TransactionRules.EnsureValid(expense, dollars, null, Groceries, Food));
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CategoryIsSubcategory))]
    public void Группа_в_качестве_категории_операции_отвергается()
    {
        Account card = Given.Account();
        Transaction expense = Transaction.Create(
            TransactionKind.Expense, card.Key, Given.Rubles(100m), null, null, Food.Key, null,
            Given.Today, null, Given.Today, Given.NowUtc);

        DomainException error = Assert.Throws<DomainException>(
            () => TransactionRules.EnsureValid(expense, card, null, Food, Food));

        Assert.Equal(Invariant.CategoryIsSubcategory, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CategoryIsSubcategory))]
    public void Подкатегория_принимается()
    {
        Account card = Given.Account();
        Transaction expense = Given.Expense(card, Groceries);

        TransactionRules.EnsureValid(expense, card, null, Groceries, Food);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransactionNotBeforeAccountOpened))]
    public void Операция_раньше_открытия_счёта_списания_отвергается()
    {
        Account card = Given.Account(openedOn: Given.Today);
        Transaction expense = Transaction.Create(
            TransactionKind.Expense, card.Key, Given.Rubles(100m), null, null, Groceries.Key, null,
            Given.Today.AddDays(-1), null, Given.Today, Given.NowUtc);

        DomainException error = Assert.Throws<DomainException>(
            () => TransactionRules.EnsureValid(expense, card, null, Groceries, Food));

        Assert.Equal(Invariant.TransactionNotBeforeAccountOpened, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransactionNotBeforeAccountOpened))]
    public void Перевод_раньше_открытия_счёта_зачисления_отвергается()
    {
        Account from = Given.Account("Карта", openedOn: Given.LongAgo);
        Account to = Given.Account("Вклад", openedOn: Given.Today);
        Transaction transfer = Given.Transfer(from, to, occurredOn: Given.Today.AddDays(-1));

        DomainException error = Assert.Throws<DomainException>(
            () => TransactionRules.EnsureValid(transfer, from, to, null, null));

        Assert.Equal(Invariant.TransactionNotBeforeAccountOpened, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransactionNotBeforeAccountOpened))]
    public void Операция_в_день_открытия_счёта_принимается()
    {
        Account card = Given.Account(openedOn: Given.Today);
        Transaction expense = Given.Expense(card, Groceries);

        TransactionRules.EnsureValid(expense, card, null, Groceries, Food);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ClosedAccountNotInNewTransaction))]
    public void Закрытый_счёт_в_новой_операции_отвергается()
    {
        Account closed = Given.Account(closed: true);
        Transaction expense = Given.Expense(closed, Groceries);

        DomainException error = Assert.Throws<DomainException>(
            () => TransactionRules.EnsureValid(expense, closed, null, Groceries, Food));

        Assert.Equal(Invariant.ClosedAccountNotInNewTransaction, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ClosedAccountNotInNewTransaction))]
    public void Уже_записанная_операция_закрытого_счёта_правится_свободно()
    {
        Account closed = Given.Account(closed: true);
        Transaction expense = Given.Expense(closed, Groceries);

        // Счёт не менялся — значит операция была записана, пока он был открыт
        TransactionRules.EnsureValid(expense, closed, null, Groceries, Food, previous: expense);
    }

    [Fact]
    public void Прежнее_состояние_от_другой_операции_отвергается()
    {
        // Ошибка вызывающего кода: подай сюда чужую операцию — и её счета сошли бы
        // за «уже задействованные», сняв запрет на закрытый счёт
        Account closed = Given.Account(closed: true);
        Transaction expense = Given.Expense(closed, Groceries);
        Transaction alien = Given.Expense(closed, Groceries);

        Assert.Throws<ArgumentException>(
            () => TransactionRules.EnsureValid(expense, closed, null, Groceries, Food, previous: alien));
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ClosedAccountNotInNewTransaction))]
    public void Смена_счёта_на_закрытый_отвергается()
    {
        Account open = Given.Account("Карта");
        Account closed = Given.Account("Старый", closed: true);
        Transaction expense = Given.Expense(open, Groceries);
        Transaction before = Given.Snapshot(expense);

        expense.Replace(
            TransactionKind.Expense, closed.Key, Given.Rubles(100m), null, null,
            Groceries.Key, null, Given.Today, null, Given.Today);

        DomainException error = Assert.Throws<DomainException>(
            () => TransactionRules.EnsureValid(expense, closed, null, Groceries, Food, previous: before));

        Assert.Equal(Invariant.ClosedAccountNotInNewTransaction, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.ClosedAccountNotInNewTransaction))]
    public void Разворот_направления_записанного_перевода_разрешён()
    {
        // Счетов не добавилось, они лишь поменялись местами — запрещать нечего
        Account closed = Given.Account("Старый", closed: true);
        Account open = Given.Account("Карта");
        Transaction transfer = Given.Transfer(closed, open);
        Transaction before = Given.Snapshot(transfer);

        transfer.Replace(
            TransactionKind.Transfer, open.Key, Given.Rubles(100m), closed.Key, Given.Rubles(100m),
            null, null, Given.Today, null, Given.Today);

        TransactionRules.EnsureValid(transfer, open, closed, null, null, previous: before);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CategoryKindMatchesTransaction))]
    public void Расход_в_доходной_категории_отвергается()
    {
        Account card = Given.Account();
        Transaction expense = Transaction.Create(
            TransactionKind.Expense, card.Key, Given.Rubles(100m), null, null, Wage.Key, null,
            Given.Today, null, Given.Today, Given.NowUtc);

        DomainException error = Assert.Throws<DomainException>(
            () => TransactionRules.EnsureValid(expense, card, null, Wage, Salary));

        Assert.Equal(Invariant.CategoryKindMatchesTransaction, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CategoryKindMatchesTransaction))]
    public void Доход_в_доходной_категории_принимается()
    {
        Account card = Given.Account();
        Transaction income = Transaction.Create(
            TransactionKind.Income, card.Key, Given.Rubles(100m), null, null, Wage.Key, null,
            Given.Today, null, Given.Today, Given.NowUtc);

        TransactionRules.EnsureValid(income, card, null, Wage, Salary);
    }

    [Fact]
    public void Перевод_между_валютами_проходит_проверки()
    {
        Account rubles = Given.Account("Карта");
        Account dollars = Given.Account("Валютный", Currency.USD);
        Transaction transfer = Given.Transfer(rubles, dollars, amount: 9000m, targetAmount: 100m);

        TransactionRules.EnsureValid(transfer, rubles, dollars, null, null);
    }
}

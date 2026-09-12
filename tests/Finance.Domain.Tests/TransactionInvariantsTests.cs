namespace Finance.Domain.Tests;

/// <summary>Правила операции, которые она проверяет сама, без счетов и категорий.</summary>
public sealed class TransactionInvariantsTests
{
    private static readonly Account Card = Given.Account();
    private static readonly Account Cash = Given.Account("Наличные");
    private static readonly Category Food = Given.Group();
    private static readonly Category Groceries = Given.Subcategory(Food);

    [Fact]
    [Trait("Инвариант", nameof(Invariant.AmountSignComesFromKind))]
    public void Расход_хранится_положительной_суммой_знак_задаёт_вид()
    {
        Transaction expense = Given.Expense(Card, Groceries, 250m);

        Assert.True(expense.Amount.IsPositive);
        Assert.Equal(TransactionKind.Expense, expense.Kind);
    }

    [Theory]
    [Trait("Инвариант", nameof(Invariant.AmountIsPositive))]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.01")]
    public void Неположительная_сумма_отвергается(string value)
    {
        decimal amount = Given.Amount(value);

        DomainException error = Assert.Throws<DomainException>(() => Given.Expense(Card, Groceries, amount));

        Assert.Equal(Invariant.AmountIsPositive, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.AmountIsPositive))]
    public void Положительная_сумма_принимается()
    {
        Assert.Equal(Given.Rubles(0.01m), Given.Expense(Card, Groceries, 0.01m).Amount);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TargetOnlyInTransfer))]
    public void У_расхода_счёта_зачисления_не_бывает()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m),
            Cash.Key, null, Groceries.Key, null, Given.Today, null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.TargetOnlyInTransfer, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TargetOnlyInTransfer))]
    public void У_перевода_счёт_зачисления_обязателен()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Transfer, Card.Key, Given.Rubles(100m),
            null, Given.Rubles(100m), null, null, Given.Today, null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.TargetOnlyInTransfer, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TargetOnlyInTransfer))]
    public void У_перевода_сумма_зачисления_обязательна_даже_в_одной_валюте()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Transfer, Card.Key, Given.Rubles(100m),
            Cash.Key, null, null, null, Given.Today, null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.TargetOnlyInTransfer, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TargetOnlyInTransfer))]
    public void Перевод_с_обоими_полями_принимается()
    {
        Transaction transfer = Given.Transfer(Card, Cash);

        Assert.Equal(Cash.Key, transfer.TargetAccountKey);
        Assert.Equal(Given.Rubles(100m), transfer.TargetAmount);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransferAccountsDiffer))]
    public void Перевод_на_тот_же_счёт_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(() => Given.Transfer(Card, Card));

        Assert.Equal(Invariant.TransferAccountsDiffer, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.SameCurrencyTransferAmountsEqual))]
    public void В_одной_валюте_суммы_перевода_обязаны_совпадать()
    {
        DomainException error = Assert.Throws<DomainException>(
            () => Given.Transfer(Card, Cash, amount: 100m, targetAmount: 99m));

        Assert.Equal(Invariant.SameCurrencyTransferAmountsEqual, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.SameCurrencyTransferAmountsEqual))]
    public void В_разных_валютах_суммы_перевода_различаются_свободно()
    {
        Account dollars = Given.Account("Валютный", Currency.USD);

        Transaction transfer = Given.Transfer(Card, dollars, amount: 9000m, targetAmount: 100m);

        Assert.Equal(Money.Create(100m, Currency.USD), transfer.TargetAmount);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CategoryOnlyInIncomeAndExpense))]
    public void Расход_без_категории_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m),
            null, null, null, null, Given.Today, null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.CategoryOnlyInIncomeAndExpense, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.CategoryOnlyInIncomeAndExpense))]
    public void Перевод_с_категорией_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Transfer, Card.Key, Given.Rubles(100m),
            Cash.Key, Given.Rubles(100m), Groceries.Key, null, Given.Today, null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.CategoryOnlyInIncomeAndExpense, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransactionDateInRange))]
    public void Дата_в_будущем_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null, Groceries.Key, null,
            Given.Today.AddDays(1), null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.TransactionDateInRange, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransactionDateInRange))]
    public void Дата_раньше_двухтысячного_года_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null, Groceries.Key, null,
            new DateOnly(1999, 12, 31), null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.TransactionDateInRange, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.TransactionDateInRange))]
    public void Сегодняшняя_дата_принимается()
    {
        Assert.Equal(Given.Today, Given.Expense(Card, Groceries).OccurredOn);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.NoteWithinLimit))]
    public void Заметка_длиннее_предела_отвергается()
    {
        string note = new('я', Transaction.MaxNoteLength + 1);

        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null, Groceries.Key, null,
            Given.Today, note, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.NoteWithinLimit, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.NoteWithinLimit))]
    public void Заметка_предельной_длины_принимается()
    {
        string note = new('я', Transaction.MaxNoteLength);

        Transaction expense = Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null, Groceries.Key, null,
            Given.Today, note, Given.Today, Given.NowUtc);

        Assert.Equal(note, expense.Note);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.EditReplacesWholeState))]
    public void Правка_заменяет_состояние_целиком_включая_вид()
    {
        Transaction expense = Given.Expense(Card, Groceries, 100m);

        expense.Replace(
            TransactionKind.Transfer, Card.Key, Given.Rubles(500m),
            Cash.Key, Given.Rubles(500m), null, null, Given.Today.AddDays(-1), "перевод", Given.Today);

        Assert.Equal(TransactionKind.Transfer, expense.Kind);
        Assert.Equal(Given.Rubles(500m), expense.Amount);
        Assert.Equal(Given.Today.AddDays(-1), expense.OccurredOn);
        Assert.Equal("перевод", expense.Note);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.PlaceNotInTransfer))]
    public void Перевод_с_местом_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(() => Transaction.Create(
            TransactionKind.Transfer, Card.Key, Given.Rubles(100m), Cash.Key, Given.Rubles(100m),
            null, Guid.NewGuid(), Given.Today, null, Given.Today, Given.NowUtc));

        Assert.Equal(Invariant.PlaceNotInTransfer, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.PlaceNotInTransfer))]
    public void Расход_без_места_и_с_местом_принимается()
    {
        Guid place = Keys.New();

        Transaction withPlace = Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null, Groceries.Key, place,
            Given.Today, null, Given.Today, Given.NowUtc);

        Assert.Equal(place, withPlace.PlaceKey);
        Assert.Null(Given.Expense(Card, Groceries).PlaceKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.AmountWithinLimit))]
    public void Сумма_сверх_предела_отвергается()
    {
        DomainException error = Assert.Throws<DomainException>(
            () => Given.Expense(Card, Groceries, Money.Limit + 0.01m));

        Assert.Equal(Invariant.AmountWithinLimit, error.Invariant);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.AmountWithinLimit))]
    public void Предельная_сумма_принимается()
    {
        Assert.Equal(Money.Limit, Given.Expense(Card, Groceries, Money.Limit).Amount.Amount);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.KindChangeClearsForbiddenFields))]
    public void Переход_в_перевод_стирает_категорию_и_место()
    {
        Transaction expense = Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null, Groceries.Key, Keys.New(),
            Given.Today, null, Given.Today, Given.NowUtc);

        expense.Replace(
            TransactionKind.Transfer, Card.Key, Given.Rubles(100m),
            Cash.Key, Given.Rubles(100m), null, null, Given.Today, null, Given.Today);

        Assert.Null(expense.CategoryKey);
        Assert.Null(expense.PlaceKey);
    }

    [Fact]
    [Trait("Инвариант", nameof(Invariant.KindChangeClearsForbiddenFields))]
    public void Переход_из_перевода_стирает_счёт_и_сумму_зачисления()
    {
        Transaction transfer = Given.Transfer(Card, Cash);

        transfer.Replace(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m),
            null, null, Groceries.Key, null, Given.Today, null, Given.Today);

        Assert.Null(transfer.TargetAccountKey);
        Assert.Null(transfer.TargetAmount);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("category")]
    [InlineData("place")]
    [InlineData("source")]
    public void Пустой_ключ_отвергается(string which)
    {
        // Форма, отдавшая невыбранное значение как default(Guid) вместо null,
        // записала бы операцию со ссылкой в никуда
        Assert.Throws<ArgumentException>(() => which switch
        {
            "target" => Transaction.Create(
                TransactionKind.Transfer, Card.Key, Given.Rubles(100m), Guid.Empty, Given.Rubles(100m),
                null, null, Given.Today, null, Given.Today, Given.NowUtc),
            "category" => Transaction.Create(
                TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null,
                Guid.Empty, null, Given.Today, null, Given.Today, Given.NowUtc),
            "place" => Transaction.Create(
                TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null,
                Groceries.Key, Guid.Empty, Given.Today, null, Given.Today, Given.NowUtc),
            _ => Transaction.Create(
                TransactionKind.Expense, Guid.Empty, Given.Rubles(100m), null, null,
                Groceries.Key, null, Given.Today, null, Given.Today, Given.NowUtc)
        });
    }

    [Fact]
    public void Пустая_заметка_не_хранится()
    {
        Transaction expense = Transaction.Create(
            TransactionKind.Expense, Card.Key, Given.Rubles(100m), null, null, Groceries.Key, null,
            Given.Today, "   ", Given.Today, Given.NowUtc);

        Assert.Null(expense.Note);
    }
}

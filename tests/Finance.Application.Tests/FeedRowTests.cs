using Finance.Application.Features.Feed;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Texts;
using Finance.Domain.Enums;
using Finance.Domain.Values;

namespace Finance.Application.Tests;

/// <summary>
/// Подпись строки ленты: что стоит под заголовком и где заметка. Сборка строки —
/// чистая функция от строки ленты, база ей не нужна.
/// </summary>
public sealed class FeedRowTests
{
    private const string LongNote = "Продукты на всю неделю в большом гипермаркете у дома";

    /// <summary>
    /// Заметка — своей строкой: группа и счёт в подписи остаются при любой её длине.
    /// В одной строке длинная заметка вытесняла группу и срезала счёт в конце.
    /// </summary>
    [Fact]
    public void Заметка_не_вытесняет_группу_и_счёт()
    {
        FeedRowItem row = FeedRowItem.From(Expense(note: LongNote), showAccount: true);

        Assert.Equal("Еда · Карта", row.CaptionText);
        Assert.Equal(LongNote, row.Note);
        Assert.True(row.HasNote);
    }

    /// <summary>
    /// Без заметки третьей строки нет, подпись та же.
    /// </summary>
    [Fact]
    public void Без_заметки_третьей_строки_нет()
    {
        FeedRowItem row = FeedRowItem.From(Expense(note: null), showAccount: true);

        Assert.Equal("Еда · Карта", row.CaptionText);
        Assert.False(row.HasNote);
    }

    /// <summary>
    /// В ленте счёта счёт в подписи не повторяется, место стоит вместо группы.
    /// </summary>
    [Fact]
    public void В_ленте_счёта_подпись_без_счёта_и_место_вместо_группы()
    {
        FeedRowItem row = FeedRowItem.From(Expense(note: LongNote, place: "Пятёрочка"), showAccount: false);

        Assert.Equal("Пятёрочка", row.Caption);
        Assert.Equal("Пятёрочка", row.CaptionText);
        Assert.Null(row.Account);
        Assert.Equal(LongNote, row.Note);
    }

    /// <summary>
    /// В общей ленте счёт — не словом в подписи, а жетоном с названием после текста:
    /// текст подписи отдельно, знак счёта отдельно.
    /// </summary>
    [Fact]
    public void В_общей_ленте_счёт_идёт_жетоном_после_текста()
    {
        FeedRowItem row = FeedRowItem.From(Expense(note: null), showAccount: true);

        Assert.Equal("Еда", row.Caption);
        Assert.True(row.HasCaptionText);
        Assert.Equal(new AccountMark("Карта", AccountColor.Blue, "credit-card"), row.Account);
        Assert.Null(row.TargetAccount);
    }

    /// <summary>
    /// Группа с тем же названием, что у подкатегории, в подписи не повторяется:
    /// в общей ленте остаётся счёт, в ленте счёта подпись пуста. Место — остаётся.
    /// </summary>
    [Fact]
    public void Группа_с_названием_подкатегории_не_повторяется()
    {
        FeedItem unsorted = Expense(note: null) with { Title = "Без категории", Group = "Без категории" };

        Assert.Equal("Карта", FeedRowItem.From(unsorted, showAccount: true).CaptionText);
        Assert.Equal(string.Empty, FeedRowItem.From(unsorted, showAccount: false).Caption);
        Assert.Equal("Пятёрочка", FeedRowItem.From(unsorted with { Place = "Пятёрочка" }, showAccount: false).Caption);
    }

    /// <summary>
    /// У перевода в общей ленте заголовок — «Перевод», подпись — направление,
    /// заметка — третьей строкой, а не вместо заголовка.
    /// </summary>
    [Fact]
    public void Заметка_перевода_идёт_третьей_строкой()
    {
        FeedItem transfer = Expense(note: LongNote) with
        {
            Kind = TransactionKind.Transfer,
            Title = "Наличные",
            Group = null,
            OtherAccount = new AccountMark("Наличные", AccountColor.Orange, "cash")
        };

        FeedRowItem common = FeedRowItem.From(transfer, showAccount: true);

        Assert.Equal(UiTexts.KindTransfer, common.Title);
        Assert.Equal("Карта → Наличные", common.CaptionText);
        Assert.Equal(AccountColor.Blue, common.Account?.Color);
        Assert.Equal(AccountColor.Orange, common.TargetAccount?.Color);
        Assert.Equal(LongNote, common.Note);

        FeedRowItem own = FeedRowItem.From(transfer, showAccount: false);

        Assert.Equal("Наличные", own.Title);
        Assert.Equal(UiTexts.KindTransfer, own.Caption);
        Assert.Equal(LongNote, own.Note);
    }

    private static FeedItem Expense(string? note, string? place = null) => new()
    {
        Key = Guid.CreateVersion7(),
        Kind = TransactionKind.Expense,
        OccurredOn = new DateOnly(2026, 9, 15),
        Amount = Money.Create(-250m, Currency.RUB),
        AccountKey = Guid.CreateVersion7(),
        AccountName = "Карта",
        Account = new AccountMark("Карта", AccountColor.Blue, "credit-card"),
        Title = "Продукты",
        Group = "Еда",
        Place = place,
        Note = note,
        Icon = "shopping-cart"
    };
}

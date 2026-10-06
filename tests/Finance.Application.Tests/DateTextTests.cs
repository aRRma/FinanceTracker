using Finance.Application.Infrastructure;
using Finance.Domain.Errors;

namespace Finance.Application.Tests;

/// <summary>
/// Как дата выглядит в тексте для пользователя.
/// </summary>
public sealed class DateTextTests
{
    /// <summary>
    /// Домен пишет дату в сообщении своим шаблоном — прикладной формат ему недоступен.
    /// Разойдись шаблоны, форма показала бы «3 февраля 2026», а ошибка под ней —
    /// ту же дату иначе.
    /// </summary>
    [Fact]
    public void Дата_в_сообщении_правила_как_на_экране()
    {
        DateOnly date = new(2026, 2, 3);

        string message = RuleTexts.Format(RuleText.TransactionNotBeforeAccountOpened, date, "Карта", date);

        Assert.Contains(DateText.DayWithYear(date), message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Шапка дня в ленте пишет год только у чужого года: за этот он был бы шумом в каждой шапке.
    /// </summary>
    [Fact]
    public void Год_пишется_только_у_чужого_года()
    {
        DateOnly today = new(2026, 9, 15);

        Assert.Equal("3 февраля", DateText.DayWithYearIfOther(new DateOnly(2026, 2, 3), today));
        Assert.Equal("3 февраля 2025", DateText.DayWithYearIfOther(new DateOnly(2025, 2, 3), today));
    }
}

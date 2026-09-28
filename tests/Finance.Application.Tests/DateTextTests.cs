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
}

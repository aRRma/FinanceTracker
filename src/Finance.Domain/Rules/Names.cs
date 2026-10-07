using Finance.Domain.Errors;

namespace Finance.Domain.Rules;

/// <summary>
/// Имена счетов, категорий и мест: приведение и сравнение.
/// </summary>
public static class Names
{
    /// <summary>
    /// Обрезает окружающие пробелы и проверяет, что имя не пусто.
    /// Обрезка именно здесь, а не в форме ввода: имя, пришедшее с необрезанным
    /// пробелом, выглядит в списке точно так же, как уже существующее, и проверка
    /// уникальности его пропустит.
    /// </summary>
    /// <param name="name">Имя, пришедшее от пользователя.</param>
    /// <param name="what">Что именно именуется — попадёт в текст ошибки.</param>
    /// <param name="qualifier">Уточнение подписи, например название группы подкатегории.</param>
    /// <exception cref="DomainException">Имя пусто или состоит из одних пробелов.</exception>
    public static string Normalize(string? name, RuleText what, string? qualifier = null)
    {
        // if, а не ThrowIf: анализ потока должен увидеть, что дальше name не null
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                Invariant.NameTrimmedAndNotEmpty,
                RuleTexts.Format(RuleText.NameEmpty, RuleTexts.Of(what, qualifier)));
        }

        return name.Trim();
    }

    /// <summary>
    /// Имена считаются одинаковыми без учёта регистра и окружающих пробелов.
    /// Правило сравнения закодировано здесь, а не у каждой проверки: второе такое
    /// место разошлось бы с этим при первой правке. Исключение — замороженная
    /// миграция, которая повторяет правило в SQL: она не может звать этот код.
    /// </summary>
    public static bool AreSame(string? left, string? right) =>
        left.AsSpan().Trim().Equals(right.AsSpan().Trim(), StringComparison.OrdinalIgnoreCase);
}

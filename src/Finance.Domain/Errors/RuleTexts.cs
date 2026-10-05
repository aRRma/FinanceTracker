using System.Globalization;
using System.Resources;

namespace Finance.Domain.Errors;

/// <summary>
/// Тексты о нарушенных правилах. Домен собирает сообщение сам — оно часть
/// правила, а не оформления экрана, — но берёт слова из ресурса, а не из кода.
/// </summary>
/// <remarks>
/// Фасад написан руками, а не сгенерирован из <c>.resx</c>: домену нужен шаблон
/// с подстановками, который разворачивается только при нарушении, а
/// сгенерированное свойство отдавало бы готовую строку на каждой проверке.
/// </remarks>
public static class RuleTexts
{
    private static readonly ResourceManager Manager =
        new("Finance.Domain.Errors.RuleTexts", typeof(RuleTexts).Assembly);

    /// <summary>
    /// Язык сообщений. Ставится прикладным слоем при запуске; по умолчанию —
    /// русский, язык, на котором приложение написано.
    /// </summary>
    public static CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// Текст по ключу.
    /// </summary>
    /// <param name="text">Ключ текста.</param>
    /// <returns>Строка ресурса; для ненайденного ключа — его имя: машинное имя правила говорит больше пустой строки.</returns>
    public static string Of(RuleText text) => Manager.GetString(text.ToString(), Culture) ?? text.ToString();

    /// <summary>
    /// Подпись предмета проверки с уточнением: «подкатегория группы «Еда»».
    /// </summary>
    /// <param name="text">Ключ подписи.</param>
    /// <param name="qualifier">Уточнение; пусто — подпись берётся как есть.</param>
    public static string Of(RuleText text, string? qualifier) =>
        qualifier is null ? Of(text) : string.Format(Culture, Of(text), qualifier);

    /// <summary>
    /// Собирает текст с подстановками. Значение <see cref="RuleText"/> среди них
    /// разворачивается в свой текст — так в предложение попадает подпись предмета проверки,
    /// а дата пишется словами, как на экране: «3 февраля 2026».
    /// </summary>
    /// <remarks>
    /// Шаблон даты живёт здесь, а не в ресурсе, по той же причине, что и на экране:
    /// порядок дня и месяца — правило языка, а не слова предложения. С датой экрана
    /// его сверяет прикладной тест, домену прикладной формат недоступен.
    /// </remarks>
    /// <param name="text">Ключ текста.</param>
    /// <param name="arguments">Подставляемые значения.</param>
    public static string Format(RuleText text, params ReadOnlySpan<object?> arguments)
    {
        object?[] resolved = new object?[arguments.Length];

        for (int i = 0; i < arguments.Length; i++)
        {
            resolved[i] = arguments[i] switch
            {
                RuleText nested => Of(nested),
                DateOnly date => date.ToString("d MMMM yyyy", Culture),
                var other => other,
            };
        }

        return string.Format(Culture, Of(text), resolved);
    }
}

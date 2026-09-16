using System.Globalization;
using Finance.Domain.Errors;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Культура интерфейса: язык текстов, склонение счётных форм и разбор дат.
/// Одна точка на всё приложение — иначе одна и та же дата на двух экранах
/// оказалась бы записана по-разному.
/// </summary>
/// <remarks>
/// Держатель статический, а не служба из набора: <c>Money.Display</c> —
/// extension-член, его зовут из плоских моделей чтения и из тестов, куда
/// внедрять нечего. Значение приходит снаружи параметром <c>AddFinance</c>,
/// как тема и версия приложения.
/// </remarks>
public static class UiCulture
{
    /// <summary>
    /// Язык приложения, пока единственный. Параметр заведён заранее: перевод
    /// иначе переписывал бы каждое место форматирования заново.
    /// </summary>
    public static CultureInfo Current { get; private set; } = Default;

    /// <summary>
    /// Как выглядит сумма: разделитель разрядов и знаков после запятой.
    /// </summary>
    /// <remarks>
    /// Разделители заданы явно, а не берутся у культуры: для русского ICU отдаёт
    /// узкий неразрывный пробел, а макеты, тесты и шрифт приложения рассчитаны
    /// на обычный неразрывный.
    /// </remarks>
    public static NumberFormatInfo Money { get; private set; } = BuildMoney();

    /// <summary>
    /// Русский — язык, на котором приложение написано, и запасной для любого
    /// незнакомого выбора.
    /// </summary>
    private static CultureInfo Default => CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// Ставит культуру приложения. Зовётся один раз при сборке служб: смена
    /// языка на ходу потребовала бы перечитать уже показанные экраны.
    /// </summary>
    /// <param name="culture">Выбранный язык; пусто — язык по умолчанию.</param>
    internal static void Use(CultureInfo? culture)
    {
        Current = culture ?? Default;
        Money = BuildMoney();

        // Домен показывает свои сообщения тому же пользователю и на том же языке,
        // но про выбор языка не знает: его делает приложение
        RuleTexts.Culture = Current;
    }

    /// <summary>
    /// Собирает формат сумм. Пересобирается вместе с культурой: число знаков
    /// после запятой у денег своё, а не то, что принято в языке.
    /// </summary>
    private static NumberFormatInfo BuildMoney() => new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ",",
        NumberDecimalDigits = 2
    };
}

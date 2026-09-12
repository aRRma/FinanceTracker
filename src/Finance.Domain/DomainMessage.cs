using System.Runtime.CompilerServices;

namespace Finance.Domain;

/// <summary>
/// Текст нарушения правила, собираемый лениво. Нужен только
/// <see cref="DomainException.ThrowIf(bool, Invariant, DomainMessage)"/>.
/// </summary>
[InterpolatedStringHandler]
public ref struct DomainMessage
{
    private DefaultInterpolatedStringHandler inner;
    private readonly bool enabled;

    /// <summary>
    /// Готовит текст, только если правило нарушено. <paramref name="shouldAppend" />
    /// возвращается компилятору: при <c>false</c> он пропускает всю сборку строки
    /// целиком, а не вызывает пустые <c>Append*</c> по одному на подстановку.
    /// </summary>
    /// <param name="literalLength">Длина постоянной части — передаёт компилятор.</param>
    /// <param name="formattedCount">Число подстановок — передаёт компилятор.</param>
    /// <param name="broken">Нарушено ли правило.</param>
    /// <param name="shouldAppend">Собирать ли текст — забирает компилятор.</param>
    public DomainMessage(int literalLength, int formattedCount, bool broken, out bool shouldAppend)
    {
        enabled = broken;
        shouldAppend = broken;
        inner = broken ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    /// <summary>Добавляет постоянную часть текста. Вызывается только при нарушении.</summary>
    public void AppendLiteral(string value) => inner.AppendLiteral(value);

    /// <summary>Добавляет подставляемое значение. Вызывается только при нарушении.</summary>
    public void AppendFormatted<T>(T value) => inner.AppendFormatted(value);

    /// <summary>Добавляет подставляемое значение с форматом. Вызывается только при нарушении.</summary>
    public void AppendFormatted<T>(T value, string? format) => inner.AppendFormatted(value, format);

    /// <summary>Собранный текст. Пустой, если правило не нарушено.</summary>
    public override string ToString() => enabled ? inner.ToStringAndClear() : string.Empty;
}

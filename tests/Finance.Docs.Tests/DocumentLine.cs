namespace Finance.Docs.Tests;

/// <summary>
/// Строка документа вместе с адресом. Проверки документации падают списком мест,
/// и без адреса такой список бесполезен: искать фразу по репозиторию дороже, чем
/// исправить её.
/// </summary>
/// <param name="File">Путь от корня репозитория, в прямых слэшах.</param>
/// <param name="Number">Номер строки, считая с единицы.</param>
/// <param name="Text">Сама строка.</param>
internal readonly record struct DocumentLine(string File, int Number, string Text)
{
    /// <summary>
    /// Адрес строки в виде <c>docs/use-cases.md:42</c>.
    /// </summary>
    public override string ToString() => $"{File}:{Number}";
}

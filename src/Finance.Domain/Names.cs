namespace Finance.Domain;

/// <summary>Имена счетов, категорий и мест: приведение и сравнение.</summary>
public static class Names
{
    /// <summary>
    /// Обрезает окружающие пробелы и проверяет, что имя не пусто.
    /// Обрезка именно здесь, а не в форме ввода: имя, пришедшее с необрезанным
    /// пробелом, выглядит в списке точно так же, как уже существующее, и проверка
    /// уникальности его пропустит.
    /// </summary>
    /// <exception cref="DomainException">Имя пусто или состоит из одних пробелов.</exception>
    public static string Normalize(string? name, string what)
    {
        // if, а не ThrowIf: анализ потока должен увидеть, что дальше name не null
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(Invariant.NameTrimmedAndNotEmpty, $"Имя ({what}) не может быть пустым");
        }

        return name.Trim();
    }

    /// <summary>
    /// Имена считаются одинаковыми без учёта регистра и окружающих пробелов.
    /// Единственное место, где закодировано правило сравнения, — второе такое
    /// место разошлось бы с этим при первой правке.
    /// </summary>
    public static bool AreSame(string? left, string? right) =>
        left.AsSpan().Trim().Equals(right.AsSpan().Trim(), StringComparison.OrdinalIgnoreCase);
}

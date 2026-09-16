using Finance.Domain.Errors;

namespace Finance.Domain.Rules;

/// <summary>
/// Проверка уникальности имён. Отдельный тип, потому что уникальность —
/// свойство набора, а не записи: одна сущность про своих соседей ничего не знает.
/// </summary>
/// <remarks>
/// Занятые имена подаются уже прочитанными: домену запрещено ходить в хранилище.
/// Область поиска у каждой сущности своя и задаётся вызывающей стороной —
/// счёт сравнивается со всеми счетами, группа с группами своего вида, подкатегория
/// с соседями внутри группы, место со всем справочником.
/// </remarks>
public static class NameUniqueness
{
    /// <summary>
    /// Проверяет, что имя свободно. Мягко удалённые записи в область поиска не
    /// входят: удалённое место не мешает завести новое с тем же названием.
    /// </summary>
    /// <param name="name">Проверяемое имя, до приведения.</param>
    /// <param name="takenNames">Имена соседей по области поиска, без удалённых.</param>
    /// <param name="what">Что именно проверяется — попадёт в текст ошибки.</param>
    public static void Ensure(string name, IEnumerable<string> takenNames, string what)
    {
        ArgumentNullException.ThrowIfNull(takenNames);

        string normalized = Names.Normalize(name, what);

        // Сообщение строится после цикла: иначе на справочник из N мест собиралось бы
        // N текстов, чтобы бросить не больше одного
        foreach (string taken in takenNames)
        {
            if (Names.AreSame(taken, normalized))
            {
                throw new DomainException(Invariant.NameUnique, $"Имя «{normalized}» уже занято ({what})");
            }
        }
    }

    /// <summary>
    /// Имена соседей для переименования: та же область поиска, но без самой
    /// переименовываемой записи — иначе она всегда конфликтовала бы сама с собой.
    /// </summary>
    /// <param name="name">Новое имя.</param>
    /// <param name="neighbours">Соседи по области поиска, ключ и имя.</param>
    /// <param name="self">Ключ переименовываемой записи.</param>
    /// <param name="what">Что именно проверяется — попадёт в текст ошибки.</param>
    public static void EnsureForRename(
        string name,
        IEnumerable<(Guid Key, string Name)> neighbours,
        Guid self,
        string what)
    {
        ArgumentNullException.ThrowIfNull(neighbours);

        Ensure(name, neighbours.Where(n => n.Key != self).Select(static n => n.Name), what);
    }
}

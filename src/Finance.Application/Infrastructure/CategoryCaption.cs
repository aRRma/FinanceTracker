using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Подпись выбранной подкатегории там, где группа не видна: в строке-поле формы
/// операции и на панели частых. Живёт в общей инфраструктуре: обе подписи
/// обязаны говорить одинаково, а это разные слайсы.
/// </summary>
public static class CategoryCaption
{
    /// <summary>
    /// Подпись подкатегории. «Прочее» — с группой: «Прочее» есть в каждой группе,
    /// и одно слово не говорит, чьё оно, а одним касанием группы теперь выбирают
    /// именно его. Приёмник с названием своей группы («Без категории») группу
    /// не повторяет. Остальные подкатегории — одним названием.
    /// </summary>
    /// <param name="name">Название подкатегории.</param>
    /// <param name="groupName">Название группы.</param>
    /// <param name="role">Роль подкатегории.</param>
    public static string Of(string name, string groupName, CategoryRole role)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(groupName);

        // Без учёта регистра, но и без культуры устройства: сравнение имён
        // не должно зависеть от языка телефона
        return role is CategoryRole.Other && !string.Equals(name, groupName, StringComparison.OrdinalIgnoreCase)
            ? string.Create(UiCulture.Current, $"{groupName} · {name}")
            : name;
    }
}

using Finance.Domain.Enums;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Счёт, как его называет строка чужого экрана: название и знак — цвет с значком.
/// Общий тип, а не поля строки: жетон в ленте и предпросмотр на экране выбора цвета
/// обязаны рисовать счёт одинаково.
/// </summary>
/// <param name="Name">Наименование счёта.</param>
/// <param name="Color">Цвет счёта — заливка знака.</param>
/// <param name="Icon">Ключ значка: выбранный руками или по типу.</param>
public sealed record AccountMark(string Name, AccountColor Color, string Icon);

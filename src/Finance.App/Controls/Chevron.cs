namespace Finance.App.Controls;

/// <summary>
/// Поворот шеврона при развороте группы. Список сам плавно раздвигает строки,
/// а знак рядом с ними менялся бы мгновенно — разнобой виден и читается как
/// подмена картинки вместо одного движения.
/// </summary>
internal static class Chevron
{
    /// <summary>
    /// Имя знака в ячейке: по нему его и находят среди соседних значков.
    /// </summary>
    internal const string Name = "Chevron";

    private const double Quarter = 90d;
    private const uint Milliseconds = 140;

    /// <summary>
    /// Доворачивает знак и применяет разворот. Ключ подменяется на довёрнутом
    /// знаке, угол сбрасывается тем же кадром: <c>chevron-right</c>, повёрнутый
    /// на четверть, и есть <c>chevron-down</c>, поэтому подмена не видна.
    /// </summary>
    /// <param name="sender">Источник касания: ячейка строки или её жест.</param>
    /// <param name="expanded">Группа развёрнута сейчас — знак поедет обратно.</param>
    /// <param name="apply">Сам разворот или сворачивание группы.</param>
    internal static async Task TurnAsync(object? sender, bool expanded, Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);

        // Знака может не найтись: у строки подкатегории его нет вовсе, а sender
        // приходит то ячейкой, то её жестом. Разворот обязан случиться всё равно —
        // потерянная анимация лучше неработающего касания
        if (Cell(sender) is not { } cell || Find(cell) is not { } chevron)
        {
            apply();

            return;
        }

        await chevron.RotateToAsync(expanded ? -Quarter : Quarter, Milliseconds, Easing.CubicOut);

        apply();

        chevron.Rotation = 0;
    }

    // Событие жеста приходит то от самого распознавателя, то от элемента,
    // на котором он висит: разметка одна, а поведение платформы разное
    private static Layout? Cell(object? sender) => sender switch
    {
        Layout cell => cell,
        GestureRecognizer { Parent: Layout cell } => cell,
        _ => null
    };

    private static Icon? Find(Layout cell)
    {
        foreach (IView child in cell.Children)
        {
            if (child is Icon { AutomationId: Name } chevron)
            {
                return chevron;
            }
        }

        return null;
    }
}

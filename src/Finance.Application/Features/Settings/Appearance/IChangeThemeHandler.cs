using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Features.Settings.Appearance;

/// <summary>Выбор темы оформления. Модель представления зовёт его напрямую.</summary>
public interface IChangeThemeHandler
{
    /// <summary>
    /// Запоминает тему и применяет её немедленно: выбор проверяют глазами,
    /// и подтверждением служит сам перекрасившийся экран.
    /// </summary>
    /// <param name="theme">Выбранная тема.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    Task HandleAsync(Theme theme, CancellationToken cancellationToken = default);
}

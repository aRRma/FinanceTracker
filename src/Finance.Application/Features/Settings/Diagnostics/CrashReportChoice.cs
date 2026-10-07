using Finance.Application.Infrastructure.Diagnostics;

namespace Finance.Application.Features.Settings.Diagnostics;

/// <summary>
/// Отчёт, выбранный в списке, — его показывает экран отчёта.
/// </summary>
/// <remarks>
/// Ключа у отчёта нет, а текст в адрес перехода не положить — он длинный. Поэтому выбор лежит здесь,
/// как выбор счёта для формы операции: список кладёт, экран отчёта забирает при появлении.
/// </remarks>
public sealed class CrashReportChoice
{
    /// <summary>
    /// Выбранный отчёт; пусто — ничего не выбрано.
    /// </summary>
    public CrashReport? Current { get; set; }
}

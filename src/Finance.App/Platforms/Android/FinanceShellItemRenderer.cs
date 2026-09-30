using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Finance.App;

/// <summary>
/// Вкладки с панелью внизу. Отличается от штатных одним: область страниц
/// не заливается чёрным на время перехода.
/// </summary>
/// <remarks>
/// Поведение сверено по исходникам <c>dotnet/maui</c> (ветка <c>net10.0</c>,
/// <c>ShellItemRendererBase.HandleFragmentUpdate</c>).
/// </remarks>
internal sealed class FinanceShellItemRenderer : ShellItemRenderer
{
    /// <summary>
    /// Создаёт обработчик вкладок.
    /// </summary>
    /// <param name="shellContext">Каркас навигации, которому принадлежат вкладки.</param>
    public FinanceShellItemRenderer(IShellContext shellContext)
        : base(shellContext)
    {
    }

    /// <summary>
    /// На время анимированного перехода MAUI заливает область страниц чёрным.
    /// Шапка у нас прозрачная — под строкой состояния видна подложка окна цветом
    /// шапки (<c>MainActivity</c>), — и чёрное просвечивало сквозь неё на каждом
    /// переходе. Без заливки под сдвигающимися страницами видна та же подложка.
    /// </summary>
    protected override Task<bool> HandleFragmentUpdate(ShellNavigationSource navSource, ShellSection shellSection, Page page, bool animated)
    {
        Task<bool> result = base.HandleFragmentUpdate(navSource, shellSection, page, animated);

        GetNavigationTarget()?.Background = null;

        return result;
    }
}

using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Finance.App;

/// <summary>
/// Каркас навигации со своим обработчиком вкладок. Сам по себе ничего не меняет:
/// он нужен только затем, чтобы вкладки собирал <see cref="FinanceShellItemRenderer"/>.
/// </summary>
internal sealed class FinanceShellRenderer : ShellRenderer
{
    /// <inheritdoc />
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem) =>
        new FinanceShellItemRenderer(this);
}

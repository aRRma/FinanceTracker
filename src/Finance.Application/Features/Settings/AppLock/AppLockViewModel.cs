using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure.AppLock;

namespace Finance.Application.Features.Settings.AppLock;

/// <summary>
/// Экран «Защита входа»: выключенная предлагает задать ПИН-код, включённая — сменить его или выключить.
/// </summary>
/// <remarks>
/// Строки, а не переключатель: включение и выключение уводят на экран ПИН-кода,
/// и переключатель стоял бы в новом положении до того, как дело сделано.
/// </remarks>
public sealed partial class AppLockViewModel : ObservableObject
{
    private readonly AppLockService _appLock;

    /// <summary>
    /// Создаёт модель экрана защиты входа.
    /// </summary>
    /// <param name="appLock">Защита входа.</param>
    public AppLockViewModel(AppLockService appLock)
    {
        ArgumentNullException.ThrowIfNull(appLock);

        _appLock = appLock;
    }

    /// <summary>
    /// Защита включена.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisabled))]
    public partial bool IsEnabled { get; private set; }

    /// <summary>
    /// Защита выключена.
    /// </summary>
    public bool IsDisabled => !IsEnabled;

    /// <summary>
    /// Перечитывает состояние — при каждом появлении: экран ПИН-кода меняет его, уходя.
    /// </summary>
    public void Load() => IsEnabled = _appLock.IsEnabled;
}

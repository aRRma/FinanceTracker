using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Application.Infrastructure.Settings;

namespace Finance.Application.Features.Settings.Appearance;

/// <summary>
/// Экран оформления: три состояния вместо переключателя. Выбранное применяется
/// сразу — пользователь видит результат, не уходя с экрана.
/// </summary>
public sealed partial class AppearanceViewModel : ScreenViewModel
{
    private readonly ISettingsSummaryQuery _summary;
    private readonly IChangeThemeHandler _change;

    /// <summary>
    /// Создаёт модель представления экрана оформления.
    /// </summary>
    /// <param name="summary">Состояние настроек.</param>
    /// <param name="change">Выбор темы.</param>
    /// <param name="changes">Оповещение об изменении данных.</param>
    public AppearanceViewModel(
        ISettingsSummaryQuery summary,
        IChangeThemeHandler change,
        IChangeNotifier changes)
        : base(changes)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(change);

        _summary = summary;
        _change = change;
    }

    /// <summary>
    /// Три темы: как в системе, светлая, тёмная.
    /// </summary>
    public ObservableCollection<ThemeOption> Options { get; } = [];

    /// <summary>
    /// Выбранная сейчас тема.
    /// </summary>
    [ObservableProperty]
    public partial Theme Current { get; private set; } = Theme.System;

    /// <summary>
    /// Перечитывает выбор.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом наполняется привязанная
        // коллекция, а её правка вне потока интерфейса роняет разметку
        SettingsSummary summary = await _summary.ReadAsync(cancellationToken);

        Current = summary.Theme;

        Rebuild();
    }

    /// <summary>
    /// Ставит тему, выбранную в списке.
    /// </summary>
    /// <param name="theme">Выбранная тема.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task SelectAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        if (theme == Current)
        {
            return;
        }

        await _change.HandleAsync(theme, cancellationToken);

        Current = theme;

        Rebuild();
    }

    private void Rebuild()
    {
        Options.Clear();

        Options.Add(Option(Theme.System, "world"));
        Options.Add(Option(Theme.Light, "sun"));
        Options.Add(Option(Theme.Dark, "moon"));
    }

    private ThemeOption Option(Theme theme, string icon) => new()
    {
        Theme = theme,
        Caption = theme.Caption,
        Icon = icon,
        IsSelected = theme == Current
    };

    /// <inheritdoc />
    protected override DataChange Watched => DataChange.Settings;

    /// <inheritdoc />
    protected override Task ReloadAsync() => LoadAsync();
}

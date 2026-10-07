using Finance.Application.Features.Settings.Diagnostics;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.App.Pages;

/// <summary>
/// Экран одного отчёта о сбое. Отчёт берёт модель из выбора в списке, здесь — окно «Поделиться»
/// и раскрытие стека.
/// </summary>
public sealed partial class CrashReportPage : DataPage
{
    private readonly CrashReportViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель экрана.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public CrashReportPage(CrashReportViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync()
    {
        _model.Load();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Читается один раз: после окна «Поделиться» раскрытый стек не сворачивается.
    /// </summary>
    protected override bool ReloadsOnAppearing => false;

    /// <summary>
    /// Отчёт — из файла, а не из базы: открывается и тогда, когда база не поднялась.
    /// </summary>
    protected override bool ReadsDatabase => false;

    private void OnStackTapped(object? sender, TappedEventArgs e) => _model.ToggleStack();

    private void OnShareClicked(object? sender, EventArgs e) => Guarded.Run(ShareAsync);

    private async Task ShareAsync()
    {
        if (await _model.PrepareShareAsync(FileSystem.CacheDirectory) is not { } file)
        {
            return;
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = UiTexts.CrashReportTitle,
            File = new ShareFile(file, "text/plain")
        });
    }
}

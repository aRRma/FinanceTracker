using Finance.Application.Features.Settings.Diagnostics;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.App.Pages;

/// <summary>
/// Экран отчётов о сбоях. Подготовка файла и очистка — в модели, здесь — окно «Поделиться» и вопрос.
/// </summary>
public sealed partial class CrashReportsPage : DataPage
{
    private readonly CrashReportsViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель экрана.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public CrashReportsPage(CrashReportsViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    /// <summary>
    /// Читается один раз: появление приходит и после окна «Поделиться», и перечитывание
    /// свернуло бы раскрытый отчёт и вернуло список к началу. Новый отчёт, пока экран
    /// открыт, приходит только вместе со сбоем.
    /// </summary>
    protected override bool ReloadsOnAppearing => false;

    private void OnReportTapped(object? sender, TappedEventArgs e)
    {
        // Отправитель — то ячейка, то распознаватель: контекст у обоих один
        if (sender is BindableObject { BindingContext: CrashReportItem item })
        {
            CrashReportsViewModel.Toggle(item);
        }
    }

    private void OnShareClicked(object? sender, EventArgs e) => Guarded.Run(ShareAsync);

    private void OnClearClicked(object? sender, EventArgs e) => Guarded.Run(ClearAsync);

    private async Task ShareAsync()
    {
        string file = await _model.PrepareShareAsync(FileSystem.CacheDirectory);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = UiTexts.CrashReportsTitle,
            File = new ShareFile(file, "text/plain")
        });
    }

    private async Task ClearAsync()
    {
        // Последствий, кроме самого удаления, нет — вопрос без текста
        if (await DisplayAlertAsync(UiTexts.CrashReportsClearQuestion, null, UiTexts.CommonDelete, UiTexts.CommonCancel))
        {
            await _model.ClearAsync(FileSystem.CacheDirectory);
        }
    }
}

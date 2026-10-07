using Finance.Application.Features.Settings.Diagnostics;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.App.Pages;

/// <summary>
/// Экран отчётов о сбоях. Сводка, файл для отправки и очистка — в модели, здесь — окно «Поделиться»,
/// вопрос и переход к отчёту.
/// </summary>
public sealed partial class CrashReportsPage : DataPage
{
    private readonly CrashReportsViewModel _model;

    // Вопрос об очистке открыт или отчёты удаляются
    private bool _clearing;

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
    /// Читается один раз: появление приходит и после экрана отчёта, и после окна «Поделиться»,
    /// а пересборка списка вернула бы прокрутку к началу. Новый отчёт, пока экран открыт,
    /// приходит только вместе со сбоем.
    /// </summary>
    protected override bool ReloadsOnAppearing => false;

    /// <summary>
    /// Отчёты лежат в своём файле, а не в базе: после сорванной миграции они нужнее всего,
    /// и ждать базы, которая не поднимется, экран не должен.
    /// </summary>
    protected override bool ReadsDatabase => false;

    private void OnReportTapped(object? sender, TappedEventArgs e)
    {
        // Отправитель — то ячейка, то распознаватель: контекст у обоих один
        if (sender is BindableObject { BindingContext: CrashReportItem item })
        {
            _model.Choose(item);
            Navigator.Go(Routes.CrashReport);
        }
    }

    private void OnLatestTapped(object? sender, TappedEventArgs e)
    {
        if (_model.Latest is { } latest)
        {
            _model.Choose(latest);
            Navigator.Go(Routes.CrashReport);
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
        // Второе касание до ответа поставило бы второй вопрос за первым — проверено на эмуляторе
        if (_clearing)
        {
            return;
        }

        _clearing = true;

        try
        {
            // Последствий, кроме самого удаления, нет — вопрос без текста
            if (await DisplayAlertAsync(UiTexts.CrashReportsClearQuestion, null, UiTexts.CommonDelete, UiTexts.CommonCancel))
            {
                await _model.ClearAsync(FileSystem.CacheDirectory);
            }
        }
        finally
        {
            _clearing = false;
        }
    }
}

using Finance.Application.Features.Settings.About;
using Finance.Application.Features.WalletImport;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-09: «О программе».
/// </summary>
public sealed partial class AboutPage : DataPage
{
    private readonly AboutViewModel _model;
    private readonly WalletImportViewModel _import;
    private bool _busy;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления экрана «О программе».</param>
    /// <param name="import">Модель строки разового переноса из Wallet.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public AboutPage(AboutViewModel model, WalletImportViewModel import, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(import);

        InitializeComponent();

        _model = model;
        _import = import;
        BindingContext = model;
        WalletImportCard.BindingContext = import;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => Task.WhenAll(_model.LoadAsync(), _import.LoadAsync());

    private void OnWalletImport(object? sender, TappedEventArgs e) => Guarded.Run(ImportAsync);

    /// <summary>
    /// Выбор файла, вопрос с числами из него и запись. Выбор файла — средство MAUI,
    /// поэтому он здесь; чтение и запись — в модели, где их проверяют тесты.
    /// </summary>
    private async Task ImportAsync()
    {
        // Сторожится весь сценарий, а не одна запись: до записи идут выбор файла
        // и два диалога, и второе касание в это время открыло бы второй выбор
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            await PickAndImportAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task PickAndImportAsync()
    {
        // Без отбора по типу: присланный через мессенджер файл приходит то как JSON,
        // то как безымянные байты, и отбор спрятал бы как раз нужный. Чужой файл
        // отвергнет разбор
        FileResult? picked = await FilePicker.Default.PickAsync();

        if (picked is null)
        {
            return;
        }

        bool readable;

        await using (Stream stream = await picked.OpenReadAsync())
        {
            readable = await _import.ReadAsync(stream);
        }

        if (!readable)
        {
            await DisplayAlertAsync(UiTexts.WalletImportUnreadable, null, UiTexts.CommonClose);
            return;
        }

        if (!await DisplayAlertAsync(
                UiTexts.WalletImportConfirmTitle, _import.ConfirmText, UiTexts.WalletImportConfirm, UiTexts.CommonCancel))
        {
            return;
        }

        string done = await _import.ImportAsync();

        await DisplayAlertAsync(UiTexts.WalletImportDoneTitle, done, UiTexts.CommonClose);
    }
}

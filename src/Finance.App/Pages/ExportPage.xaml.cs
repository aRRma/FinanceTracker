using Finance.Application.Features.Export;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.App.Pages;

/// <summary>
/// Экран D-10: «Данные» — выгрузка в файл и восстановление из него.
/// </summary>
public sealed partial class ExportPage : DataPage
{
    private readonly ExportViewModel _model;
    private bool _busy;

    // Ответ на открытое предупреждение и номер его отсчёта
    private TaskCompletionSource<bool>? _answer;
    private int _countdown;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления экрана.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public ExportPage(ExportViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => Task.CompletedTask;

    private void OnExportToDevice(object? sender, TappedEventArgs e) => Guarded.Run(() => ExclusiveAsync(ExportToDeviceAsync));

    private void OnExport(object? sender, TappedEventArgs e) => Guarded.Run(() => ExclusiveAsync(ExportAsync));

    private void OnRecover(object? sender, TappedEventArgs e) => Guarded.Run(() => ExclusiveAsync(RecoverAsync));

    /// <summary>
    /// Сторожит сценарий целиком, а не одну запись: до неё идут выбор файла и
    /// диалоги, и второе касание в это время открыло бы второй выбор.
    /// </summary>
    private async Task ExclusiveAsync(Func<Task> scenario)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            await scenario();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ExportToDeviceAsync()
    {
        string file = await _model.ExportAsync();

        // Отказ в окне выбора места — не ошибка: пользователь передумал
        if (await DeviceFileSaver.SaveAsync(file))
        {
            Notice.Show(UiTexts.ExportSaved);
        }
    }

    private async Task ExportAsync()
    {
        string file = await _model.ExportAsync();

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = UiTexts.ExportShareTitle,
            File = new ShareFile(file, "application/octet-stream")
        });
    }

    /// <summary>
    /// Выбор файла, проверка, вопрос с числами обеих сторон, запись и перезапуск.
    /// Проверка и запись — в модели, где их проверяют тесты.
    /// </summary>
    private async Task RecoverAsync()
    {
        // Без отбора по типу: присланный через мессенджер файл приходит то с типом
        // базы, то безымянными байтами, и отбор спрятал бы как раз нужный.
        // Чужой файл отвергнет проверка
        FileResult? picked = await FilePicker.Default.PickAsync();

        if (picked is null)
        {
            return;
        }

        bool ready;

        try
        {
            await using Stream stream = await picked.OpenReadAsync();
            ready = await _model.CheckAsync(stream);
        }
        finally
        {
            ForgetPickedCopy(picked);
        }

        if (!ready)
        {
            await DisplayAlertAsync(_model.RefusalText, null, UiTexts.CommonClose);
            return;
        }

        // Пустую базу заменять не страшно: после переустановки человек просто
        // возвращает свои данные, и вопрос был бы лишним
        if (_model.ReplacesData && !await WarnAsync())
        {
            _model.Discard();
            return;
        }

        // Сорвавшаяся запись не должна оставить на экране предупреждение с мёртвыми
        // кнопками: ответа уже никто не ждёт, а проверенный файл больше не нужен
        bool recovered = false;

        try
        {
            await _model.RecoverAsync();
            recovered = true;
        }
        finally
        {
            if (!recovered)
            {
                _model.Discard();
            }
        }

        await DisplayAlertAsync(UiTexts.RecoveryDoneTitle, UiTexts.RecoveryDoneText, UiTexts.RecoveryRestart);

        // Открытые экраны, стеки вкладок и запомненные при запуске пояс и тема
        // описывают прежние данные: дальше — только с чистого запуска
        AppRestart.Run();
    }

    /// <summary>
    /// Показывает предупреждение и ждёт ответа. Секунды отсчитывает таймер
    /// платформы; номер отсчёта не даёт таймеру прошлого предупреждения,
    /// закрытого и открытого заново за секунду, отсчитывать вдвое быстрее.
    /// </summary>
    private async Task<bool> WarnAsync()
    {
        _answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int countdown = ++_countdown;

        _model.ShowWarning();
        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () => countdown == _countdown && _model.Tick());

        try
        {
            return await _answer.Task;
        }
        finally
        {
            _answer = null;
        }
    }

    private void OnWarningConfirm(object? sender, EventArgs e) => _answer?.TrySetResult(true);

    private void OnWarningCancel(object? sender, EventArgs e) => _answer?.TrySetResult(false);

    /// <inheritdoc />
    protected override bool OnBackButtonPressed()
    {
        // «Назад» закрывает предупреждение, а не уводит с экрана: так ведёт
        // себя любой диалог Android
        if (_answer is not null)
        {
            _answer.TrySetResult(false);

            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        // Ушёл стрелкой в шапке или вкладкой — значит, передумал
        _answer?.TrySetResult(false);

        base.OnDisappearing();
    }

    /// <summary>
    /// Удаляет копию, которую выбор файла MAUI кладёт в кэш приложения: модель
    /// уже приняла файл к себе, а вся история не должна оставаться в папке,
    /// которую пользователь не видит. Файл вне кэша — сам выбранный, его не трогаем.
    /// </summary>
    private static void ForgetPickedCopy(FileResult picked)
    {
        // Сравниваются канонические пути: папка данных приложения видна и как
        // /data/user/0/…, и как /data/data/…, и простое сравнение строк промахивалось
        try
        {
            string cache = new Java.IO.File(FileSystem.CacheDirectory).CanonicalPath;
            string copy = new Java.IO.File(picked.FullPath).CanonicalPath;

            if (copy.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                File.Delete(copy);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Java.IO.IOException)
        {
            // Уборка не повод срывать восстановление: копию уберёт система вместе с кэшем
        }
    }
}

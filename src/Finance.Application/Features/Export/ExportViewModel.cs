using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.Application.Features.Export;

/// <summary>
/// Экран «Данные»: выгрузка в файл и восстановление из него. Выбор файла,
/// «Поделиться», таймер и перезапуск — средства платформы, их делает страница;
/// здесь всё, что проверяется тестами без эмулятора.
/// </summary>
/// <remarks>
/// Восстановление поверх данных необратимо, поэтому перед ним — предупреждение,
/// которое нельзя подтвердить сразу: кнопка оживает после отсчёта. На пустой
/// базе терять нечего, и предупреждения нет.
/// </remarks>
public sealed partial class ExportViewModel : ObservableObject
{
    /// <summary>
    /// Сколько секунд кнопка замены остаётся неактивной: столько, чтобы
    /// предупреждение успели прочитать, а не смахнули по привычке.
    /// </summary>
    public const int WarningSeconds = 5;

    private readonly IExportHandler _export;
    private readonly IRecoveryHandler _recovery;

    /// <summary>
    /// Создаёт модель экрана.
    /// </summary>
    /// <param name="export">Выгрузка.</param>
    /// <param name="recovery">Восстановление из выгрузки.</param>
    public ExportViewModel(IExportHandler export, IRecoveryHandler recovery)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(recovery);

        _export = export;
        _recovery = recovery;
    }

    /// <summary>
    /// Идёт выгрузка, проверка файла или запись: экран показывает ожидание.
    /// На многолетней истории это секунды, и без знака касание казалось бы несработавшим.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>
    /// На экране предупреждение перед заменой вместо строк выгрузки и восстановления.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActionsShown))]
    public partial bool IsWarningShown { get; private set; }

    /// <summary>
    /// Сколько секунд кнопке замены осталось быть неактивной.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm), nameof(ConfirmButtonText))]
    public partial int SecondsLeft { get; private set; }

    /// <summary>
    /// Видны строки выгрузки и восстановления: предупреждения нет.
    /// </summary>
    public bool IsActionsShown => !IsWarningShown;

    /// <summary>
    /// Отсчёт кончился, и замену можно подтвердить.
    /// </summary>
    public bool CanConfirm => SecondsLeft == 0;

    /// <summary>
    /// Подпись кнопки замены: пока идёт отсчёт — с оставшимися секундами.
    /// </summary>
    public string ConfirmButtonText => SecondsLeft > 0
        ? string.Format(UiCulture.Current, UiTexts.RecoveryConfirmWaiting, SecondsLeft)
        : UiTexts.RecoveryConfirm;

    /// <summary>
    /// В базе есть что терять: перед заменой нужно предупреждение. Известно после проверки файла.
    /// </summary>
    public bool ReplacesData { get; private set; }

    /// <summary>
    /// Почему файл не подошёл. Пусто, если подошёл или ещё не проверен.
    /// </summary>
    public string RefusalText { get; private set; } = string.Empty;

    /// <summary>
    /// Что удалит замена, по строке на счета, операции, свои категории и места.
    /// Строки с нулём нет: «0 мест» — шум. Пусто, пока файл не проверен.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<RecoveryLine> ToDelete { get; private set; } = [];

    /// <summary>
    /// Записывает выгрузку.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Полный путь к файлу выгрузки.</returns>
    public Task<string> ExportAsync(CancellationToken cancellationToken = default) =>
        RunAsync(() => _export.HandleAsync(cancellationToken));

    /// <summary>
    /// Проверяет выбранный файл и готовит предупреждение перед заменой или текст отказа.
    /// </summary>
    /// <param name="file">Содержимое файла.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><see langword="true"/>, если файл подходит.</returns>
    public async Task<bool> CheckAsync(Stream file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        // Сбой проверки не должен оставить тексты прошлого файла
        Forget();

        RecoveryCheck check = await RunAsync(() => _recovery.CheckAsync(file, cancellationToken));

        RefusalText = check.Verdict switch
        {
            RecoveryVerdict.Ready => string.Empty,
            RecoveryVerdict.Damaged => UiTexts.RecoveryDamaged,
            RecoveryVerdict.Newer => UiTexts.RecoveryNewer,
            _ => UiTexts.RecoveryNotExport
        };

        if (check.Current is { } current)
        {
            ReplacesData = current.HasData;
            ToDelete = LinesOf(current);
        }

        return check.Verdict is RecoveryVerdict.Ready;
    }

    /// <summary>
    /// Показывает предупреждение и запускает отсчёт. Секунды отсчитывает
    /// страница таймером платформы, вызывая <see cref="Tick"/>.
    /// </summary>
    public void ShowWarning()
    {
        SecondsLeft = WarningSeconds;
        IsWarningShown = true;
    }

    /// <summary>
    /// Отсчитывает одну секунду.
    /// </summary>
    /// <returns><see langword="true"/>, пока отсчёт не кончился и таймеру есть что делать.</returns>
    public bool Tick()
    {
        if (SecondsLeft > 0)
        {
            SecondsLeft--;
        }

        return SecondsLeft > 0;
    }

    /// <summary>
    /// Заменяет данные базы проверенным файлом. После этого приложение перезапускается.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <exception cref="InvalidOperationException">Отсчёт предупреждения ещё не кончился.</exception>
    public Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        // Последний рубеж: погашенную кнопку не нажать, но и обходной путь
        // мимо отсчёта — ошибка вызывающего, а не тихая замена
        if (!CanConfirm)
        {
            throw new InvalidOperationException(Faults.RecoveryWarningRunning());
        }

        return RunAsync(() => _recovery.RecoverAsync(cancellationToken));
    }

    /// <summary>
    /// Забывает проверенный файл и закрывает предупреждение: пользователь передумал.
    /// </summary>
    public void Discard()
    {
        _recovery.Discard();
        Forget();
    }

    private void Forget()
    {
        IsWarningShown = false;
        SecondsLeft = 0;
        ReplacesData = false;
        RefusalText = string.Empty;
        ToDelete = [];
    }

    private static List<RecoveryLine> LinesOf(RecoverySide current)
    {
        List<RecoveryLine> lines = [];

        void Add(int count, string icon, string one, string few, string many)
        {
            if (count > 0)
            {
                lines.Add(new RecoveryLine { Icon = icon, Text = Plural.Of(count, one, few, many), HasDivider = lines.Count > 0 });
            }
        }

        Add(current.Accounts, "wallet", UiTexts.RecoveryAccountsOne, UiTexts.RecoveryAccountsFew, UiTexts.RecoveryAccountsMany);
        Add(current.Transactions, "receipt", UiTexts.RecoveryTransactionsOne, UiTexts.RecoveryTransactionsFew, UiTexts.RecoveryTransactionsMany);
        Add(current.Categories, "tag", UiTexts.RecoveryCategoriesOne, UiTexts.RecoveryCategoriesFew, UiTexts.RecoveryCategoriesMany);
        Add(current.Places, "map-pin", UiTexts.RecoveryPlacesOne, UiTexts.RecoveryPlacesFew, UiTexts.RecoveryPlacesMany);

        return lines;
    }

    /// <summary>
    /// Держит признак ожидания на время действия. Действие сторожит страница
    /// целиком — с выбором файла и диалогами; второй запуск здесь — ошибка вызывающего.
    /// </summary>
    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException(Faults.ExportRunning());
        }

        IsBusy = true;

        try
        {
            // ConfigureAwait(false) здесь недопустим: следом меняется привязанное свойство
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        T result = default!;

        // Тело блоком, а не выражением: лямбда с присваиванием возвращала бы Task<T>,
        // и вызов ушёл бы в эту же перегрузку — бесконечная рекурсия
        await RunAsync(async () => { result = await action(); });

        return result;
    }
}

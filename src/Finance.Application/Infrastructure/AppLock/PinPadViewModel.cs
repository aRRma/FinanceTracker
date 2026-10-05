using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Texts;

namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Набор ПИН-кода: четыре точки, цифры, стирание и пауза после неверных попыток.
/// Общий для заслонки и для задания, смены и выключения кода в «Ещё».
/// </summary>
/// <remarks>
/// Код уходит на проверку по набору последней цифры — кнопки «Готово» нет.
/// Набор и проверка разведены: цифру набирает синхронная команда клавиатуры,
/// проверку запускает экран, когда <see cref="IsFull"/>.
/// </remarks>
public abstract partial class PinPadViewModel : ObservableObject
{
    private string _typed = string.Empty;

    /// <summary>
    /// Создаёт набор ПИН-кода.
    /// </summary>
    /// <param name="appLock">Защита входа: пауза после неверных попыток.</param>
    protected PinPadViewModel(AppLockService appLock)
    {
        ArgumentNullException.ThrowIfNull(appLock);

        AppLock = appLock;
    }

    /// <summary>
    /// Защита входа.
    /// </summary>
    protected AppLockService AppLock { get; }

    /// <summary>
    /// Заголовок над точками: что набирать сейчас.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; protected set; } = string.Empty;

    /// <summary>
    /// Ответ на прошлый набор: неверный код, несовпавший повтор, пауза.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; protected set; }

    /// <summary>
    /// Есть ответ на прошлый набор.
    /// </summary>
    public bool HasMessage => Message is not null;

    /// <summary>
    /// Код проверяется: медленная функция занимает долю секунды, и набранное
    /// за это время ушло бы в следующий код.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanType))]
    public partial bool IsChecking { get; private set; }

    /// <summary>
    /// Идёт пауза после неверных попыток: клавиатура погашена.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanType))]
    public partial bool IsPaused { get; private set; }

    /// <summary>
    /// Цифры принимаются.
    /// </summary>
    public bool CanType => !IsChecking && !IsPaused;

    /// <summary>
    /// Точки: закрашенные — набранные цифры.
    /// </summary>
    public IReadOnlyList<bool> Dots
    {
        get
        {
            bool[] dots = new bool[AppLockService.PinLength];

            for (int i = 0; i < _typed.Length && i < dots.Length; i++)
            {
                dots[i] = true;
            }

            return dots;
        }
    }

    /// <summary>
    /// Набраны все цифры — пора проверять.
    /// </summary>
    public bool IsFull => _typed.Length is AppLockService.PinLength;

    /// <summary>
    /// Пауза касается этого шага: она отсчитывается от неверных попыток
    /// ввести заданный код и не мешает задавать новый.
    /// </summary>
    protected virtual bool ObeysPause => true;

    /// <summary>
    /// Набор отвергнут: экран встряхивает точки.
    /// </summary>
    public event EventHandler? Rejected;

    /// <summary>
    /// Добавляет цифру.
    /// </summary>
    /// <param name="digit">Цифра с клавиатуры.</param>
    public void Type(char digit)
    {
        if (!CanType || IsFull || !char.IsAsciiDigit(digit))
        {
            return;
        }

        _typed += digit;

        OnPropertyChanged(nameof(Dots));
    }

    /// <summary>
    /// Стирает последнюю цифру.
    /// </summary>
    public void Erase()
    {
        if (!CanType || _typed.Length is 0)
        {
            return;
        }

        _typed = _typed[..^1];

        OnPropertyChanged(nameof(Dots));
    }

    /// <summary>
    /// Проверяет набранный код. Без всех цифр и во время проверки не делает ничего.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (!IsFull || IsChecking)
        {
            return;
        }

        string pin = _typed;

        IsChecking = true;

        try
        {
            // Продолжение правит привязанные свойства — ConfigureAwait(false) нельзя
            await CompleteAsync(pin, cancellationToken);
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>
    /// Пересчитывает паузу. Экран зовёт его раз в секунду, пока идёт пауза.
    /// </summary>
    /// <returns>Пауза ещё идёт — звать снова.</returns>
    public bool Tick()
    {
        TimeSpan left = ObeysPause ? AppLock.PauseRemaining() : TimeSpan.Zero;

        if (left > TimeSpan.Zero)
        {
            IsPaused = true;
            Message = string.Format(UiCulture.Current, UiTexts.PinPause, FormatPause(left));

            return true;
        }

        if (IsPaused)
        {
            IsPaused = false;
            Message = null;
        }

        return false;
    }

    /// <summary>
    /// Что делать с набранным кодом.
    /// </summary>
    /// <param name="pin">Набранный код.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    protected abstract Task CompleteAsync(string pin, CancellationToken cancellationToken);

    /// <summary>
    /// Стирает набранное.
    /// </summary>
    protected void Clear()
    {
        _typed = string.Empty;

        OnPropertyChanged(nameof(Dots));
    }

    /// <summary>
    /// Отвергает набор: стирает его, говорит почему и встряхивает точки.
    /// </summary>
    /// <param name="message">Почему отвергнут.</param>
    protected void Reject(string message)
    {
        Clear();

        Message = message;

        Rejected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Отвечает на проверку заданного кода, кроме верного: неверный — с числом
    /// оставшихся попыток, когда их две и меньше, пауза — с отсчётом.
    /// </summary>
    /// <param name="check">Ответ защиты входа.</param>
    protected void Answer(PinCheck check)
    {
        string wrong = check.AttemptsLeft is > 0 and <= 2
            ? string.Format(
                UiCulture.Current,
                UiTexts.PinWrongAttemptsLeft,
                Plural.Of(check.AttemptsLeft, UiTexts.PinAttemptsOne, UiTexts.PinAttemptsFew, UiTexts.PinAttemptsMany))
            : UiTexts.PinWrong;

        if (check.Outcome is PinCheckOutcome.Rejected)
        {
            Reject(wrong);
        }
        else
        {
            Clear();
        }

        // Пауза началась этой попыткой или уже шла — отсчёт заменяет ответ
        Tick();
    }

    /// <summary>
    /// Остаток паузы минутами и секундами: «0:25», «14:59», «1:00:00».
    /// </summary>
    private static string FormatPause(TimeSpan left)
    {
        // Округление вверх: «0:00» при ещё идущей паузе читалось бы как сбой
        TimeSpan shown = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));

        return shown.TotalHours >= 1
            ? shown.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : shown.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}

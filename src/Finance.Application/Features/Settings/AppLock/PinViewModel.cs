using Finance.Application.Infrastructure.AppLock;
using Finance.Application.Texts;

namespace Finance.Application.Features.Settings.AppLock;

/// <summary>
/// Экран ПИН-кода в «Ещё»: задание кода с повтором, смена и выключение защиты.
/// Смена и выключение начинаются с текущего кода — неверный засчитывается попыткой,
/// как на заслонке, иначе подбирать код можно было бы здесь без пауз.
/// </summary>
public sealed class PinViewModel : PinPadViewModel
{
    private PinStep _step;
    private string? _first;

    /// <summary>
    /// Создаёт модель экрана ПИН-кода.
    /// </summary>
    /// <param name="appLock">Защита входа.</param>
    public PinViewModel(AppLockService appLock)
        : base(appLock)
    {
    }

    /// <summary>
    /// Зачем открыт экран.
    /// </summary>
    public PinPurpose Purpose { get; private set; }

    /// <summary>
    /// Дело сделано — экран уходит, а текст говорит, что изменилось.
    /// </summary>
    public event EventHandler<string>? Done;

    /// <inheritdoc />
    protected override bool ObeysPause => _step is PinStep.Current;

    /// <summary>
    /// Начинает с первого шага: смена и выключение — с текущего кода, задание — сразу с нового.
    /// </summary>
    /// <param name="purpose">Зачем открыт экран.</param>
    /// <returns>Идёт пауза — экрану запускать отсчёт.</returns>
    public bool Start(PinPurpose purpose)
    {
        // Задание поверх включённой защиты — это смена: без текущего кода
        // чужой параметр маршрута подменил бы код, которого никто не знает
        if (purpose is PinPurpose.Create && AppLock.IsEnabled)
        {
            purpose = PinPurpose.Change;
        }

        Purpose = purpose;

        if (purpose is PinPurpose.Create)
        {
            GoNew();
        }
        else
        {
            _step = PinStep.Current;
            Title = UiTexts.PinCurrentTitle;
            Message = null;

            Clear();
        }

        return Tick();
    }

    /// <inheritdoc />
    protected override async Task CompleteAsync(string pin, CancellationToken cancellationToken)
    {
        switch (_step)
        {
            case PinStep.Current:
                await CheckCurrentAsync(pin, cancellationToken);
                break;

            case PinStep.New:
                _first = pin;
                _step = PinStep.Repeat;
                Title = UiTexts.PinRepeatTitle;
                Message = null;

                Clear();
                break;

            case PinStep.Repeat when pin == _first:
                await AppLock.EnableAsync(pin, cancellationToken);

                Done?.Invoke(this, Purpose is PinPurpose.Create ? UiTexts.AppLockEnabledNotice : UiTexts.PinChangedNotice);
                break;

            default:
                GoNew();
                Reject(UiTexts.PinMismatch);
                break;
        }
    }

    private async Task CheckCurrentAsync(string pin, CancellationToken cancellationToken)
    {
        PinCheck check = await AppLock.CheckAsync(pin, cancellationToken);

        if (check.Outcome is not PinCheckOutcome.Accepted)
        {
            Answer(check);

            return;
        }

        if (Purpose is PinPurpose.Disable)
        {
            AppLock.Disable();

            Done?.Invoke(this, UiTexts.AppLockDisabledNotice);

            return;
        }

        GoNew();
    }

    private void GoNew()
    {
        _step = PinStep.New;
        _first = null;
        Title = UiTexts.PinNewTitle;
        Message = null;

        Clear();
        Tick();
    }

    private enum PinStep
    {
        Unknown = 0,
        Current = 1,
        New = 2,
        Repeat = 3
    }
}

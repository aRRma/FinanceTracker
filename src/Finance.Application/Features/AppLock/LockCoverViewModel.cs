using Finance.Application.Infrastructure.AppLock;
using Finance.Application.Texts;

namespace Finance.Application.Features.AppLock;

/// <summary>
/// Заслонка: вход по ПИН-коду при запуске и после пяти минут в фоне.
/// Верный код снимает её, и пользователь оказывается там же, где был.
/// </summary>
public sealed class LockCoverViewModel : PinPadViewModel
{
    /// <summary>
    /// Создаёт модель заслонки.
    /// </summary>
    /// <param name="appLock">Защита входа.</param>
    public LockCoverViewModel(AppLockService appLock)
        : base(appLock)
    {
        Title = UiTexts.PinEnterTitle;
    }

    /// <summary>
    /// Вход выполнен — заслонку пора убрать.
    /// </summary>
    public event EventHandler? Unlocked;

    /// <summary>
    /// Готовит заслонку к показу: чистый набор и идущая пауза.
    /// </summary>
    /// <returns>Идёт пауза — экрану запускать отсчёт.</returns>
    public bool Prepare()
    {
        Message = null;

        Clear();

        return Tick();
    }

    /// <inheritdoc />
    protected override async Task CompleteAsync(string pin, CancellationToken cancellationToken)
    {
        PinCheck check = await AppLock.CheckAsync(pin, cancellationToken);

        if (check.Outcome is PinCheckOutcome.Accepted)
        {
            Clear();

            Unlocked?.Invoke(this, EventArgs.Empty);

            return;
        }

        Answer(check);
    }
}

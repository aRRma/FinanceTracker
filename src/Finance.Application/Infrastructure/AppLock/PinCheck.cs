namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// Ответ на набранный ПИН-код.
/// </summary>
/// <param name="Outcome">Чем кончилась проверка.</param>
/// <param name="AttemptsLeft">Сколько неверных попыток осталось до первой паузы.</param>
/// <param name="Pause">Сколько ждать до следующей попытки; ноль — ждать не нужно.</param>
public readonly record struct PinCheck(PinCheckOutcome Outcome, int AttemptsLeft, TimeSpan Pause);

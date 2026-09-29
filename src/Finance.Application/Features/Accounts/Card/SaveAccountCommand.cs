using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Card;

/// <summary>
/// Что пользователь ввёл в карточке счёта. Одна команда на заведение и правку:
/// экран один и тот же, а отличает их только заполненный ключ.
/// </summary>
public sealed record SaveAccountCommand
{
    /// <summary>
    /// Ключ правимого счёта. Пусто — заводится новый.
    /// </summary>
    public Guid? Key { get; init; }

    /// <summary>
    /// Наименование счёта.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Наличные или карта.
    /// </summary>
    public required AccountType Type { get; init; }

    /// <summary>
    /// Валюта. После первой операции по счёту не меняется.
    /// </summary>
    public required Currency Currency { get; init; }

    /// <summary>
    /// Начальный остаток. Бывает отрицательным: долг по карте тоже остаток.
    /// </summary>
    public required decimal OpeningBalance { get; init; }

    /// <summary>
    /// Дата, с которой действует начальный остаток.
    /// </summary>
    public required DateOnly OpenedOn { get; init; }

    /// <summary>
    /// «Скрытый».
    /// </summary>
    public required bool ExcludedFromTotals { get; init; }

    /// <summary>
    /// «Счёт заблокирован». Обратимо, ненулевой баланс блокировке не мешает.
    /// </summary>
    public required bool IsClosed { get; init; }
}

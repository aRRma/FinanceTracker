using Finance.Domain.Enums;

namespace Finance.Application.Features.Accounts.Badge;

/// <summary>
/// Цвет и значок счёта по дороге между карточкой и экраном выбора. Карточка кладёт
/// сюда счёт, как он набран, экран меняет цвет и значок, а карточка, вернувшись,
/// забирает выбор один раз. В базу ничего не уходит до «Сохранить» в карточке.
/// <para>
/// Параметр маршрута не годится по той же причине, что у выбора в форме операции:
/// название счёта — кириллица, а параметр остаётся в свойстве страницы и подставился бы ещё раз.
/// </para>
/// </summary>
public sealed class AccountBadgeDraft
{
    /// <summary>
    /// Ключ счёта; пусто — счёт только заводится, и предпросмотра ленты у него нет.
    /// </summary>
    public Guid? Key { get; private set; }

    /// <summary>
    /// Название, как набрано в карточке.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Тип — от него значок по умолчанию.
    /// </summary>
    public AccountType Type { get; private set; }

    /// <summary>
    /// «Скрытый» — у накоплений свой значок по умолчанию.
    /// </summary>
    public bool ExcludedFromTotals { get; private set; }

    /// <summary>
    /// Баланс для строки предпросмотра, уже отформатированный.
    /// </summary>
    public string Balance { get; private set; } = string.Empty;

    /// <summary>
    /// Баланс отрицателен — в предпросмотре он смыслового цвета, как на балансах.
    /// </summary>
    public bool IsNegative { get; private set; }

    /// <summary>
    /// Цвет счёта.
    /// </summary>
    public AccountColor Color { get; set; }

    /// <summary>
    /// Значок, выбранный руками; пусто — по типу.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// На экране выбора что-то меняли — карточке есть что забрать.
    /// </summary>
    public bool IsChanged { get; set; }

    /// <summary>
    /// Карточка уходит на экран выбора: кладёт счёт, как он набран сейчас.
    /// </summary>
    /// <param name="key">Ключ счёта; пусто у нового.</param>
    /// <param name="name">Название.</param>
    /// <param name="type">Тип.</param>
    /// <param name="excludedFromTotals">«Скрытый».</param>
    /// <param name="balance">Баланс, уже отформатированный.</param>
    /// <param name="isNegative">Баланс отрицателен.</param>
    /// <param name="color">Цвет.</param>
    /// <param name="icon">Значок, выбранный руками.</param>
    public void Start(
        Guid? key,
        string name,
        AccountType type,
        bool excludedFromTotals,
        string balance,
        bool isNegative,
        AccountColor color,
        string? icon)
    {
        Key = key;
        Name = name;
        Type = type;
        ExcludedFromTotals = excludedFromTotals;
        Balance = balance;
        IsNegative = isNegative;
        Color = color;
        Icon = icon;
        IsChanged = false;
    }

    /// <summary>
    /// Забирает выбор: что поменяли на экране, достаётся карточке ровно один раз.
    /// </summary>
    /// <param name="color">Выбранный цвет.</param>
    /// <param name="icon">Выбранный значок; пусто — по типу.</param>
    /// <returns><see langword="true"/>, если на экране выбора что-то меняли.</returns>
    public bool TryTake(out AccountColor color, out string? icon)
    {
        color = Color;
        icon = Icon;

        bool changed = IsChanged;
        IsChanged = false;

        return changed;
    }
}

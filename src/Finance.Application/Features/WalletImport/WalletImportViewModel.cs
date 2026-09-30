using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Texts;

namespace Finance.Application.Features.WalletImport;

/// <summary>
/// Строка разового переноса из Wallet на экране «О программе». Видна только
/// на пустой базе и пропадает после переноса: механизм разовый, под личный перенос,
/// а не функция для всех. Файл выбирает страница — выбор файла существует только в MAUI.
/// </summary>
/// <remarks>
/// Два шага, а не один: сначала файл читается и называет, сколько запишет, потом
/// пользователь подтверждает. Перенос не отменяется, и запускать его вслепую нельзя.
/// </remarks>
public sealed partial class WalletImportViewModel : ObservableObject
{
    private readonly IWalletImportHandler _handler;
    private WalletImportFile? _pending;

    /// <summary>
    /// Создаёт модель строки переноса.
    /// </summary>
    /// <param name="handler">Перенос.</param>
    public WalletImportViewModel(IWalletImportHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
    }

    /// <summary>
    /// Показывать ли строку переноса: база пуста.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>
    /// Идёт запись: строка показывает ожидание. Перенос многолетней истории занимает
    /// секунды, и без знака пользователь решил бы, что касание не сработало.
    /// </summary>
    [ObservableProperty]
    public partial bool IsImporting { get; private set; }

    /// <summary>
    /// Вопрос перед записью: сколько чего запишется. Пусто, пока файл не прочитан.
    /// </summary>
    public string ConfirmText { get; private set; } = string.Empty;

    /// <summary>
    /// Выясняет, пуста ли база.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом меняется привязанное свойство
        IsAvailable = await _handler.IsAvailableAsync(cancellationToken);
    }

    /// <summary>
    /// Читает выбранный файл и готовит вопрос перед записью.
    /// </summary>
    /// <param name="json">Содержимое файла.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><see langword="false"/>, если файл не подходит для переноса.</returns>
    public async Task<bool> ReadAsync(Stream json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            _pending = await WalletImportFile.ReadAsync(json, cancellationToken);
        }
        catch (JsonException)
        {
            // Чужой файл — не сбой приложения, а выбор не того файла: об этом
            // говорится своими словами, а не общим «что-то пошло не так»
            _pending = null;
            ConfirmText = string.Empty;

            return false;
        }

        WalletImportCounts counts = WalletImportCounts.Of(_pending);
        ConfirmText = string.Format(
            UiCulture.Current, UiTexts.WalletImportConfirmText, counts.Accounts, counts.Places, counts.Transactions);

        return true;
    }

    /// <summary>
    /// Записывает прочитанный файл.
    /// </summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Итог для сообщения: сколько записано.</returns>
    /// <exception cref="InvalidOperationException">Файл не прочитан или запись уже идёт.</exception>
    public async Task<string> ImportAsync(CancellationToken cancellationToken = default)
    {
        WalletImportFile file = _pending ?? throw new InvalidOperationException(Faults.WalletImportFileEmpty());

        // Сценарий целиком — выбор файла, вопрос, запись — сторожит страница;
        // здесь последний рубеж, и второй запуск — ошибка вызывающего, а не тихий пропуск
        if (IsImporting)
        {
            throw new InvalidOperationException(Faults.WalletImportRunning());
        }

        IsImporting = true;

        try
        {
            WalletImportCounts written = await _handler.HandleAsync(file, cancellationToken);
            _pending = null;
            ConfirmText = string.Empty;
            IsAvailable = false;

            return string.Format(
                UiCulture.Current, UiTexts.WalletImportDoneText, written.Accounts, written.Places, written.Transactions);
        }
        finally
        {
            IsImporting = false;
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Модель представления экрана, который перечитывает себя по изменению данных.
/// Наследование здесь осознанное: правило «какие изменения требуют перечитать
/// экран» иначе повторялось бы в каждой модели, и при появлении нового вида
/// изменения одну из копий забыли бы.
/// </summary>
public abstract class ScreenViewModel : ObservableObject, IScreenModel
{
    private readonly IChangeNotifier _changes;

    /// <summary>Создаёт модель экрана.</summary>
    /// <param name="changes">Оповещение об изменении данных.</param>
    protected ScreenViewModel(IChangeNotifier changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        _changes = changes;
    }

    /// <inheritdoc />
    public event Action<Exception>? ReloadFailed;

    /// <summary>Какие изменения устаревают этот экран.</summary>
    protected abstract DataChange Watched { get; }

    /// <summary>Перечитывает экран после изменения.</summary>
    protected abstract Task ReloadAsync();

    /// <inheritdoc />
    public void Activate()
    {
        // Сначала снятие, потом подписка: на Android появление экрана приходит
        // и без предшествующего ухода — после возврата приложения из фона, —
        // и голое += копило бы вторую и третью подписку, а с ними лишние
        // перечитывания экрана на каждое изменение
        _changes.Changed -= OnChanged;
        _changes.Changed += OnChanged;
    }

    /// <inheritdoc />
    public void Deactivate() => _changes.Changed -= OnChanged;

    /// <summary>
    /// Метод async void: подписчик события иначе не бывает. Перехват поэтому
    /// обязателен — без него сбой чтения не всплыл бы к вызывающему, а уронил
    /// бы процесс, и приложение закрылось бы на чужой правке данных.
    /// </summary>
    private async void OnChanged(DataChange change)
    {
        if ((change & Watched) is DataChange.None)
        {
            return;
        }

        try
        {
            // ConfigureAwait(false) здесь недопустим: перечитывание наполняет
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            await ReloadAsync();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Молча оставить устаревший экран нельзя: пользователь увидел бы
            // прежние числа и принял бы их за нынешние
            ReloadFailed?.Invoke(error);
        }
    }
}

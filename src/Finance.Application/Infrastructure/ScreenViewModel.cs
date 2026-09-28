using CommunityToolkit.Mvvm.ComponentModel;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Модель представления экрана, который перечитывает себя по изменению данных.
/// Наследование здесь осознанное: правило «какие изменения требуют перечитать
/// экран» иначе повторялось бы в каждой модели, и при появлении нового вида
/// изменения одну из копий забыли бы.
/// </summary>
public abstract partial class ScreenViewModel : ObservableObject, IScreenModel
{
    private readonly IChangeNotifier _changes;

    // Номер изменения, которое экран уже учёл; минус один — экран ещё не появлялся
    private long _seen = -1;

    /// <summary>
    /// Создаёт модель экрана.
    /// </summary>
    /// <param name="changes">Оповещение об изменении данных.</param>
    protected ScreenViewModel(IChangeNotifier changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        _changes = changes;
    }

    /// <inheritdoc />
    public event Action<Exception>? ReloadFailed;

    /// <summary>
    /// Идёт обновление жестом «потянуть вниз». Признак принадлежит только жесту:
    /// жест поднимает его сам, а гасит <see cref="RefreshAsync"/>. Своё чтение
    /// модель им не отмечает — <c>RefreshView</c> на каждый подъём признака
    /// запускает обновление, и чтение, поднявшее его само, запускало бы второе:
    /// дочитывание ленты перечитывало её с начала.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    /// <summary>
    /// Пока экран был скрыт, изменилось то, что он показывает. Скрытый экран
    /// не подписан на изменения и узнаёт о пропущенном только так. Спрашивать
    /// до <see cref="Activate"/>: появление считает всё прежнее учтённым.
    /// </summary>
    public virtual bool IsOutdated => _seen >= 0 && _changes.VersionOf(Watched) > _seen;

    /// <summary>
    /// Какие изменения устаревают этот экран.
    /// </summary>
    protected abstract DataChange Watched { get; }

    /// <summary>
    /// Перечитывает экран после изменения.
    /// </summary>
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

        // Пропущенное экран либо перечитывает прямо сейчас, либо оно его не касалось
        _seen = _changes.VersionOf(Watched);
    }

    /// <inheritdoc />
    public void Deactivate() => _changes.Changed -= OnChanged;

    /// <inheritdoc />
    public async Task RefreshAsync()
    {
        try
        {
            // ConfigureAwait(false) здесь недопустим: перечитывание наполняет
            // привязанные коллекции, а их правка вне потока интерфейса роняет разметку
            await ReloadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

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

        // Изменение учтено на виду: возврат из фона без ухода с экрана
        // иначе принял бы его за пропущенное и перечитал экран ещё раз
        _seen = _changes.VersionOf(Watched);

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

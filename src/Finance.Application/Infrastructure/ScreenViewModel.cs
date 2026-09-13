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

    /// <summary>Какие изменения устаревают этот экран.</summary>
    protected abstract DataChange Watched { get; }

    /// <summary>Перечитывает экран после изменения.</summary>
    protected abstract void Reload();

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

    private void OnChanged(DataChange change)
    {
        if ((change & Watched) is not DataChange.None)
        {
            Reload();
        }
    }
}

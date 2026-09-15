using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain;

namespace Finance.Application.Features.Places.Card;

/// <summary>
/// Карточка места: переименование и удаление. Заведения здесь нет — место
/// появляется из формы операции, а сюда приходят исправлять опечатку или
/// убирать дубль.
/// </summary>
public sealed partial class PlaceViewModel : ObservableObject
{
    private readonly IPlacesQuery _places;
    private readonly IRenamePlaceHandler _rename;
    private readonly IDeletePlaceHandler _delete;

    private int _transactionCount;
    private string _savedName = string.Empty;

    /// <summary>Создаёт модель представления карточки места.</summary>
    /// <param name="places">Справочник мест со счётчиками.</param>
    /// <param name="rename">Переименование места.</param>
    /// <param name="delete">Удаление места.</param>
    public PlaceViewModel(IPlacesQuery places, IRenamePlaceHandler rename, IDeletePlaceHandler delete)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(rename);
        ArgumentNullException.ThrowIfNull(delete);

        _places = places;
        _rename = rename;
        _delete = delete;
    }

    /// <summary>Ключ правимого места.</summary>
    public Guid? Key { get; private set; }

    /// <summary>Название места.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Текст нарушенного правила. Пусто — сохранять можно.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>
    /// Сколько операций у места и в какой подкатегории оно чаще всего. По этой
    /// подписи дубль отличается от исходного места ещё до правки.
    /// </summary>
    [ObservableProperty]
    public partial string UsageCaption { get; private set; } = string.Empty;

    /// <summary>Правило нарушено — сообщение показывается рядом с формой.</summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Идёт сохранение или удаление. Кнопки зовут методы напрямую, минуя команду
    /// с её защитой от повторного запуска: без флага второе нажатие до ухода
    /// экрана запустило бы второе действие.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// <summary>Сохранять и удалять можно: предыдущее действие не идёт.</summary>
    public bool CanSave => !IsSaving;

    /// <summary>Место загрузилось — есть что править и что удалять.</summary>
    public bool IsLoaded => Key is not null;

    /// <summary>Загружает место для правки.</summary>
    /// <param name="key">Ключ места.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    [RelayCommand]
    public async Task LoadAsync(Guid key, CancellationToken cancellationToken = default)
    {
        // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
        // свойства, а их правка вне потока интерфейса роняет разметку
        IReadOnlyList<PlaceListItem> places = await _places.ReadAsync(cancellationToken);

        if (places.FirstOrDefault(place => place.Key == key) is not { } found)
        {
            return;
        }

        Key = found.Key;
        Name = found.Name;
        _savedName = found.Name;
        _transactionCount = found.TransactionCount;

        UsageCaption = found.TransactionCount is 0
            ? "Место ещё не использовано ни в одной операции"
            : $"{Plural.Of(found.TransactionCount, "операция", "операции", "операций")}"
              + (found.TopCategoryName is { } category ? $" · чаще всего «{category}»" : string.Empty);

        OnPropertyChanged(nameof(IsLoaded));
    }

    /// <summary>Сохраняет новое название.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если место сохранено и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Key is not { } key || IsSaving)
        {
            return false;
        }

        IsSaving = true;
        Error = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            await _rename.HandleAsync(key, Name, cancellationToken);

            // Записанное имя теперь новое: заголовок удаления обязан назвать его,
            // а не то, что было до переименования
            _savedName = Name.Trim();

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;
            OnPropertyChanged(nameof(HasError));

            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Заголовок подтверждения удаления. Имя берётся записанное, а не набранное
    /// в поле: удаляется место как оно сохранено, и правка в поле к удалению
    /// отношения не имеет.
    /// </summary>
    public string DeleteTitle => $"Удалить «{_savedName}»?";

    /// <summary>
    /// Текст подтверждения удаления. Называет число операций, которые останутся
    /// без места: по нему видно, то ли это место, а строки операций не правятся.
    /// </summary>
    public string DeletePrompt =>
        _transactionCount is 0
            ? "Операций с этим местом нет. Отменить удаление будет нельзя."
            : $"{Plural.Of(_transactionCount, "операция останется", "операции останутся", "операций останутся")}"
              + " без места. Суммы, даты и балансы не изменятся.";

    /// <summary>Удаляет место.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если место удалено и экран можно закрыть.</returns>
    [RelayCommand]
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (Key is not { } key || IsSaving)
        {
            return false;
        }

        IsSaving = true;

        try
        {
            await _delete.HandleAsync(key, cancellationToken);

            return true;
        }
        finally
        {
            IsSaving = false;
        }
    }
}

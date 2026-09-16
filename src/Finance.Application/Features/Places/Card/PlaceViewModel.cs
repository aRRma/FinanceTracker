using Finance.Application.Texts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;
using Finance.Domain.Errors;

namespace Finance.Application.Features.Places.Card;

/// <summary>
/// Карточка места: переименование и удаление. Заведения здесь нет — место
/// появляется из формы операции, а сюда приходят исправлять опечатку или
/// убирать дубль.
/// </summary>
public sealed partial class PlaceViewModel : ObservableObject, IFormModel
{
    private readonly IPlacesQuery _places;
    private readonly IRenamePlaceHandler _rename;
    private readonly IDeletePlaceHandler _delete;

    private int _transactionCount;
    private string _savedName = string.Empty;

    /// <summary>
    /// Создаёт модель представления карточки места.
    /// </summary>
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

    /// <summary>
    /// Ключ правимого места.
    /// </summary>
    public Guid? Key { get; private set; }

    /// <summary>
    /// Название места.
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// Текст нарушенного правила. Пусто — сохранять можно.
    /// </summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>
    /// Сколько операций у места и в какой подкатегории оно чаще всего. По этой
    /// подписи дубль отличается от исходного места ещё до правки.
    /// </summary>
    [ObservableProperty]
    public partial string UsageCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Правило нарушено — сообщение показывается рядом с формой.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Идёт сохранение или удаление. Кнопки зовут методы напрямую, минуя команду
    /// с её защитой от повторного запуска: без флага второе нажатие до ухода
    /// экрана запустило бы второе действие.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// Предыдущее действие закончилось — можно начинать следующее. Отдельно
    /// от <see cref="CanSave"/>: к нему привязана и кнопка удаления, а «можно
    /// сохранить» о ней ничего не говорит.
    /// </summary>
    public bool IsIdle => !IsSaving;

    /// <summary>
    /// Сохранять можно: предыдущее действие не идёт.
    /// </summary>
    public bool CanSave => IsIdle;

    /// <summary>
    /// Место загрузилось — есть что править и что удалять.
    /// </summary>
    public bool IsLoaded => Key is not null;

    /// <inheritdoc />
    public bool IsDirty => !string.Equals(Name.Trim(), _savedName, StringComparison.Ordinal);

    /// <summary>
    /// Загружает место для правки.
    /// </summary>
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

        string usage = Plural.Of(
            found.TransactionCount,
            UiTexts.PlaceUsageOne,
            UiTexts.PlaceUsageFew,
            UiTexts.PlaceUsageMany);

        UsageCaption = (found.TransactionCount, found.TopCategoryName) switch
        {
            (0, _) => UiTexts.PlaceUnused,
            (_, { } category) => string.Format(UiCulture.Current, UiTexts.PlaceTopCategory, usage, category),
            _ => usage
        };

        OnPropertyChanged(nameof(IsLoaded));
    }

    /// <summary>
    /// Сохраняет новое название.
    /// </summary>
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
        bool done = false;
        Error = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            await _rename.HandleAsync(key, Name, cancellationToken);

            // Записанное имя теперь новое: заголовок удаления обязан назвать его,
            // а не то, что было до переименования
            _savedName = Name.Trim();

            done = true;

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
            // После удачи флаг остаётся: экран закрывается, и второе нажатие
            // в этот промежуток записало бы то же самое ещё раз. При неудаче
            // он снимается — нарушенное правило правят и сохраняют снова
            IsSaving = done;
        }
    }

    /// <summary>
    /// Заголовок подтверждения удаления. Имя берётся записанное, а не набранное
    /// в поле: удаляется место как оно сохранено, и правка в поле к удалению
    /// отношения не имеет.
    /// </summary>
    public string DeleteTitle => string.Format(UiCulture.Current, UiTexts.PlaceDeleteConfirmTitle, _savedName);

    /// <summary>
    /// Текст подтверждения удаления. Называет число операций, которые останутся
    /// без места: по нему видно, то ли это место, а строки операций не правятся.
    /// </summary>
    public string DeletePrompt =>
        _transactionCount is 0
            ? UiTexts.PlaceDeleteEmpty
            : string.Format(
                UiCulture.Current,
                UiTexts.PlaceDeletePrompt,
                Plural.Of(
                    _transactionCount,
                    UiTexts.PlaceStayingOne,
                    UiTexts.PlaceStayingFew,
                    UiTexts.PlaceStayingMany));

    /// <summary>
    /// Удаляет место.
    /// </summary>
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
        bool done = false;

        try
        {
            await _delete.HandleAsync(key, cancellationToken);

            done = true;

            return true;
        }
        finally
        {
            // После удачи флаг остаётся: экран закрывается, и второе нажатие
            // в этот промежуток записало бы то же самое ещё раз. При неудаче
            // он снимается — нарушенное правило правят и сохраняют снова
            IsSaving = done;
        }
    }
}

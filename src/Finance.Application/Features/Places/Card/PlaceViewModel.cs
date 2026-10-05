using Finance.Application.Texts;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Queries;

namespace Finance.Application.Features.Places.Card;

/// <summary>
/// Карточка места: переименование и удаление. Заведения здесь нет — место
/// появляется из формы операции, а сюда приходят исправлять опечатку или
/// убирать дубль.
/// </summary>
public sealed partial class PlaceViewModel : FormViewModel
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
    /// Сколько операций у места и в какой подкатегории оно чаще всего. По этой
    /// подписи дубль отличается от исходного места ещё до правки.
    /// </summary>
    [ObservableProperty]
    public partial string UsageCaption { get; private set; } = string.Empty;

    /// <summary>
    /// Место загрузилось — есть что править и что удалять.
    /// </summary>
    public bool IsLoaded => Key is not null;

    /// <inheritdoc />
    public override bool IsDirty => !string.Equals(Name.Trim(), _savedName, StringComparison.Ordinal);

    /// <summary>
    /// Загружает место для правки.
    /// </summary>
    /// <param name="key">Ключ места.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
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
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default) =>
        Key is { } key
        && await WriteAsync(
            async token =>
            {
                await _rename.HandleAsync(key, Name, token);

                // Записанное имя теперь новое: заголовок удаления обязан назвать его,
                // а не то, что было до переименования
                _savedName = Name.Trim();
            },
            cancellationToken);

    /// <summary>
    /// Заголовок подтверждения удаления. Имя берётся записанное, а не набранное
    /// в поле: удаляется место как оно сохранено, и правка в поле к удалению
    /// отношения не имеет.
    /// </summary>
    public string DeleteTitle => string.Format(UiCulture.Current, UiTexts.PlaceDeleteConfirmTitle, _savedName);

    /// <summary>
    /// Текст подтверждения удаления. Есть операции — предупреждает, сколько их
    /// останется без места: по числу видно, то ли это место. Операций нет —
    /// текста нет вовсе, хватает заголовка с вопросом.
    /// </summary>
    public string? DeletePrompt =>
        _transactionCount is 0
            ? null
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
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default) =>
        Key is { } key && await WriteAsync(token => _delete.HandleAsync(key, token), cancellationToken);
}

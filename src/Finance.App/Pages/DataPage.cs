using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;

namespace Finance.App.Pages;

/// <summary>
/// Страница, читающая данные. Перед первым чтением дожидается подготовки базы:
/// вкладка может открыться раньше, чем накатились миграции, и запрос к ещё
/// не созданной таблице выглядел бы случайным сбоем.
/// </summary>
public abstract class DataPage : ContentPage
{
    private readonly FinanceStartup _startup;

    /// <summary>Создаёт страницу.</summary>
    /// <param name="startup">Подготовка приложения: миграции, стартовый набор, часовой пояс.</param>
    protected DataPage(FinanceStartup startup)
    {
        ArgumentNullException.ThrowIfNull(startup);

        _startup = startup;
    }

    /// <summary>Читает то, что показывает страница.</summary>
    protected abstract Task LoadAsync();

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Подписка на изменения живёт только пока экран на виду: оповещение
        // одно на приложение, и подписка от создания модели копилась бы
        // с каждым заходом на экран
        (BindingContext as IScreenModel)?.Activate();

        try
        {
            // ConfigureAwait здесь не ставится намеренно: продолжение обязано
            // вернуться в поток интерфейса — оно наполняет привязанные коллекции
            await _startup.PrepareAsync();
            await LoadAsync();
        }
        catch (DatabaseMigrationException error)
        {
            // Со старой схемой новый код работать не может, и делать вид,
            // что экран просто пуст, нельзя: данные целы, а приложение — нет
            await DisplayAlertAsync("База не обновилась", error.Message, "Закрыть");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Метод async void: невыловленный сбой чтения здесь не всплывает
            // к вызывающему, а роняет процесс. Показать сообщение — единственное,
            // что тут можно сделать, и это лучше молчаливого закрытия приложения
            await DisplayAlertAsync("Не удалось прочитать данные", error.Message, "Закрыть");
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        (BindingContext as IScreenModel)?.Deactivate();
    }
}

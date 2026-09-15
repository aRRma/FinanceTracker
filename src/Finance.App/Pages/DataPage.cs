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

    private bool _loaded;

    /// <summary>Создаёт страницу.</summary>
    /// <param name="startup">Подготовка приложения: миграции, стартовый набор, часовой пояс.</param>
    protected DataPage(FinanceStartup startup)
    {
        ArgumentNullException.ThrowIfNull(startup);

        _startup = startup;
    }

    /// <summary>Читает то, что показывает страница.</summary>
    protected abstract Task LoadAsync();

    /// <summary>
    /// Перечитывать ли страницу при каждом появлении. Списки — да: вернувшись
    /// с карточки, пользователь ждёт свежих чисел. Формы — нет: появление приходит
    /// и при возврате приложения из фона, и перечитывание стёрло бы набранное.
    /// </summary>
    protected virtual bool ReloadsOnAppearing => true;

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Стрелка в шапке мимо OnBackButtonPressed не идёт вовсе: Shell уводит
        // её своим переходом. Перехватывать приходится обе кнопки порознь
        if (BindingContext is IFormModel)
        {
            Shell.SetBackButtonBehavior(this, new BackButtonBehavior
            {
                Command = new Command(() => Guarded.Run(LeaveAsync))
            });
        }

        // Подписка на изменения живёт только пока экран на виду: оповещение
        // одно на приложение, и подписка от создания модели копилась бы
        // с каждым заходом на экран
        if (BindingContext is IScreenModel screen)
        {
            screen.ReloadFailed -= OnReloadFailed;
            screen.ReloadFailed += OnReloadFailed;
            screen.Activate();
        }

        if (ReloadsOnAppearing || !_loaded)
        {
            _loaded = true;
            Guarded.Run(PrepareAndLoadAsync);
        }
    }

    /// <summary>
    /// Перечитать экран по чужой правке не удалось. Сказать об этом обязательно:
    /// пользователь ничего не нажимал и принял бы устаревшие числа за нынешние.
    /// </summary>
    private void OnReloadFailed(Exception error) =>
        Guarded.Run(() => DisplayAlertAsync("Данные не обновились", error.Message, "Закрыть"));

    /// <summary>
    /// Подготовка базы и первое чтение. Общий перехват живёт в <see cref="Guarded"/>;
    /// здесь остаётся только неудачная миграция — у неё свой ответ.
    /// </summary>
    private async Task PrepareAndLoadAsync()
    {
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
    }

    /// <summary>
    /// Аппаратная и жестовая «назад». Возвращает <c>true</c> — «переход обработан
    /// здесь»: пока пользователь не ответил, экран остаётся на месте.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is IFormModel { IsDirty: true })
        {
            Guarded.Run(LeaveAsync);

            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>
    /// Уходит с формы, спросив про набранное. Спрашивает только когда терять
    /// есть что: вопрос на каждом выходе перестали бы читать, и однажды он
    /// увёл бы с заполненной формы вместе со всеми остальными.
    /// </summary>
    private async Task LeaveAsync()
    {
        if (BindingContext is IFormModel { IsDirty: true }
            && !await DisplayAlertAsync("Уйти без сохранения?", "Набранное не сохранится.", "Уйти", "Остаться"))
        {
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is IScreenModel screen)
        {
            screen.Deactivate();
            screen.ReloadFailed -= OnReloadFailed;
        }
    }
}

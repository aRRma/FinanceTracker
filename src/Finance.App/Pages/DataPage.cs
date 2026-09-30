using Finance.Application.Texts;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using AndroidX.Core.View;
using AView = Android.Views.View;

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

    /// <summary>
    /// Создаёт страницу.
    /// </summary>
    /// <param name="startup">Подготовка приложения: миграции, стартовый набор, часовой пояс.</param>
    protected DataPage(FinanceStartup startup)
    {
        ArgumentNullException.ThrowIfNull(startup);

        _startup = startup;
    }

    /// <summary>
    /// Читает то, что показывает страница.
    /// </summary>
    protected abstract Task LoadAsync();

    /// <summary>
    /// Перечитывать ли страницу при каждом появлении. Списки — да: вернувшись
    /// с карточки, пользователь ждёт свежих чисел. Формы — нет: появление приходит
    /// и при возврате приложения из фона, и перечитывание стёрло бы набранное.
    /// Ленты — только устаревшие: перечитывание сбрасывает прокрутку.
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

        // Решение о перечитывании — до подписки: появившийся экран считает
        // всё изменённое раньше учтённым, и устаревшим он был бы уже не виден
        bool reload = ReloadsOnAppearing || !_loaded;

        // Подписка на изменения живёт только пока экран на виду: оповещение
        // одно на приложение, и подписка от создания модели копилась бы
        // с каждым заходом на экран
        if (BindingContext is IScreenModel screen)
        {
            screen.ReloadFailed -= OnReloadFailed;
            screen.ReloadFailed += OnReloadFailed;
            screen.Activate();
        }

        if (reload)
        {
            _loaded = true;
            Guarded.Run(PrepareAndLoadAsync);
        }
    }

    /// <summary>
    /// Перечитать экран по чужой правке не удалось. Сказать об этом обязательно:
    /// пользователь ничего не нажимал и принял бы устаревшие числа за нынешние.
    /// </summary>
    private void OnReloadFailed(Exception error) => Guarded.Report(UiTexts.ErrorReloadFailedTitle, error);

    /// <summary>
    /// Жест «потянуть вниз». Обработчиком, а не привязкой команды: сбой команды,
    /// запущенной разметкой, закрыл бы окно молча, а здесь его покажет <see cref="Guarded"/>.
    /// </summary>
    protected void OnRefreshing(object? sender, EventArgs e)
    {
        if (BindingContext is IScreenModel screen)
        {
            Guarded.Run(screen.RefreshAsync);
        }
    }

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
            await DisplayAlertAsync(UiTexts.ErrorDatabaseFailedTitle, error.Message, UiTexts.CommonClose);
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
            && !await DisplayAlertAsync(
                UiTexts.LeaveWithoutSavingTitle,
                UiTexts.LeaveWithoutSavingPrompt,
                UiTexts.LeaveWithoutSavingConfirm,
                UiTexts.LeaveWithoutSavingCancel))
        {
            return;
        }

        await Navigator.GoAsync("..");
    }

    /// <inheritdoc />
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        if (args.OldHandler?.PlatformView is AView old)
        {
            old.LayoutChange -= OnPlatformLayoutChange;
        }
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler?.PlatformView is AView view)
        {
            view.LayoutChange -= OnPlatformLayoutChange;
            view.LayoutChange += OnPlatformLayoutChange;
        }
    }

    /// <summary>
    /// MAUI считает отступ под системные полосы по месту страницы на экране, но
    /// только когда отступы раздаются, а смена высоты страницы их не раздаёт.
    /// Страница, ушедшая под форму без панели вкладок, растягивалась до низа
    /// экрана и получала отступ под жестовую полосу, а вернувшись над панелью,
    /// так с ним и оставалась: «+ Операция» стояла выше на высоту полосы.
    /// Поэтому смена высоты раздаёт отступы заново — уже по новому месту.
    /// </summary>
    private static void OnPlatformLayoutChange(object? sender, AView.LayoutChangeEventArgs e)
    {
        if (sender is AView view && e.Bottom - e.Top != e.OldBottom - e.OldTop)
        {
            ViewCompat.RequestApplyInsets(view);
        }
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

using Finance.Application.Infrastructure;

namespace Finance.Application.Tests;

/// <summary>
/// Экран, перечитывающий себя по изменению данных: подписка и сбой чтения.
/// </summary>
public sealed class ScreenViewModelTests
{
    /// <summary>
    /// Перечитывается только то, что устарело от этого изменения.
    /// </summary>
    [Fact]
    public void Изменение_вне_наблюдаемых_экран_не_будит()
    {
        ChangeNotifier notifier = new();
        Screen screen = new(notifier, DataChange.Accounts);

        screen.Activate();
        notifier.Publish(DataChange.Places);

        Assert.Equal(0, screen.Reloads);
    }

    /// <summary>
    /// Сбой перечитывания доходит до экрана. Молча оставить устаревшие числа нельзя:
    /// пользователь ничего не нажимал и принял бы их за нынешние — а сам сбой
    /// в подписчике события не всплывает к вызывающему, он роняет процесс.
    /// </summary>
    [Fact]
    public void Сбой_перечитывания_не_теряется()
    {
        ChangeNotifier notifier = new();
        Screen screen = new(notifier, DataChange.Accounts) { Failure = new InvalidOperationException("база занята") };

        Exception? reported = null;
        screen.ReloadFailed += error => reported = error;

        screen.Activate();
        notifier.Publish(DataChange.Accounts);

        Assert.Equal("база занята", reported?.Message);
    }

    /// <summary>
    /// Ушедший экран изменений больше не слушает: иначе подписки копились бы с каждым заходом.
    /// </summary>
    [Fact]
    public void Ушедший_экран_не_перечитывается()
    {
        ChangeNotifier notifier = new();
        Screen screen = new(notifier, DataChange.Accounts);

        screen.Activate();
        screen.Deactivate();
        notifier.Publish(DataChange.Accounts);

        Assert.Equal(0, screen.Reloads);
    }

    /// <summary>
    /// Заготовка экрана: считает перечитывания и по требованию роняет чтение.
    /// </summary>
    private sealed class Screen(IChangeNotifier changes, DataChange watched) : ScreenViewModel(changes)
    {
        public int Reloads { get; private set; }

        public Exception? Failure { get; init; }

        protected override DataChange Watched => watched;

        protected override Task ReloadAsync()
        {
            Reloads++;

            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}

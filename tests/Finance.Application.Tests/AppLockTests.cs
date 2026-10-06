using Finance.Application.Features.AppLock;
using Finance.Application.Features.More;
using Finance.Application.Features.Settings.AppLock;
using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.AppLock;
using Finance.Application.Texts;

namespace Finance.Application.Tests;

/// <summary>
/// Защита входа: след ПИН-кода, когда спрашивать вход, пауза после неверных попыток,
/// заслонка, экран ПИН-кода и предложение при первом запуске.
/// </summary>
public sealed class AppLockTests
{
    private const string Pin = "2580";

    [Theory]
    [InlineData("1234", true)]
    [InlineData("0000", true)]
    [InlineData("123", false)]
    [InlineData("12345", false)]
    [InlineData("12a4", false)]
    [InlineData("１２３４", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ПИН_код_ровно_четыре_цифры(string? pin, bool valid) =>
        Assert.Equal(valid, AppLockService.IsValid(pin));

    /// <summary>
    /// В хранилище лежит след, а не код, и новый след каждый раз со своей солью:
    /// одинаковые коды не узнаются по одинаковым следам.
    /// </summary>
    [Fact]
    public async Task Хранится_след_а_не_код()
    {
        TestDevice device = new();
        AppLockService appLock = device.Lock();

        await appLock.EnableAsync(Pin);
        string first = Assert.IsType<string>(device.Trace);

        await appLock.EnableAsync(Pin);

        Assert.DoesNotContain(Pin, first, StringComparison.Ordinal);
        Assert.NotEqual(first, device.Trace);
        Assert.True(appLock.IsEnabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.100000.AAAA")]
    [InlineData("2.100000.AAAA.AAAA")]
    [InlineData("1.много.AAAA.AAAA")]
    [InlineData("1.100000.не base64.AAAA")]
    public void Испорченный_след_не_разбирается(string text) =>
        Assert.Null(PinTrace.Parse(text));

    [Fact]
    public async Task Верный_код_принимается_неверный_нет()
    {
        AppLockService appLock = new TestDevice().Lock();

        await appLock.EnableAsync(Pin);

        Assert.Equal(PinCheckOutcome.Rejected, (await appLock.CheckAsync("0852")).Outcome);
        Assert.Equal(PinCheckOutcome.Accepted, (await appLock.CheckAsync(Pin)).Outcome);
    }

    [Fact]
    public async Task Запуск_с_защитой_просит_вход()
    {
        TestDevice device = new();

        Assert.False(device.Lock().Start());

        await device.Lock().EnableAsync(Pin);

        AppLockService restarted = device.Lock();

        Assert.True(restarted.Start());
        Assert.True(restarted.IsLocked);
    }

    /// <summary>
    /// Меньше пяти минут в фоне — без входа: так переживают выбор файла и «Поделиться».
    /// Пять минут и дольше, в том числе во сне, — вход.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(299, false)]
    [InlineData(300, true)]
    [InlineData(3600, true)]
    public async Task Возврат_из_фона_просит_вход_через_пять_минут(int seconds, bool locked)
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        appLock.Leave();
        device.Pass(TimeSpan.FromSeconds(seconds));

        Assert.Equal(locked, appLock.Return());
    }

    /// <summary>
    /// Окно пересоздано в живом процессе — смена масштаба шрифта, возврат после
    /// выхода «назад»: это не запуск, и решает время в фоне, а не сам факт.
    /// </summary>
    [Theory]
    [InlineData(5, false)]
    [InlineData(300, true)]
    public async Task Пересоздание_окна_решает_как_возврат_из_фона(int seconds, bool locked)
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        appLock.Leave();
        device.Pass(TimeSpan.FromSeconds(seconds));

        Assert.Equal(locked, appLock.Start());
    }

    [Fact]
    public async Task Перезагрузка_в_фоне_просит_вход()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        appLock.Leave();
        device.Reboot();

        Assert.True(appLock.Return());
    }

    /// <summary>
    /// Заслонка, поднятая однажды, снимается только кодом: второй короткий уход
    /// в фон начинал бы отсчёт заново и снимал её возвратом.
    /// </summary>
    [Fact]
    public async Task Заслонку_снимает_только_код()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        appLock.Leave();
        device.Pass(TimeSpan.FromMinutes(10));
        Assert.True(appLock.Return());

        appLock.Leave();
        device.Pass(TimeSpan.FromSeconds(5));
        Assert.True(appLock.Return());

        await appLock.CheckAsync(Pin);
        Assert.False(appLock.IsLocked);
    }

    [Fact]
    public async Task Без_защиты_вход_не_нужен()
    {
        TestDevice device = new();
        AppLockService appLock = device.Lock();

        appLock.Start();
        appLock.Leave();
        device.Pass(TimeSpan.FromHours(1));

        Assert.False(appLock.Return());
    }

    /// <summary>
    /// Пять неверных подряд — без паузы, дальше перед каждой попыткой пауза растёт.
    /// Попытка во время паузы не проверяется и не засчитывается.
    /// </summary>
    [Fact]
    public async Task После_пяти_неверных_пауза_растёт()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        for (int i = 0; i < 4; i++)
        {
            PinCheck free = await appLock.CheckAsync("0000");
            Assert.Equal(TimeSpan.Zero, free.Pause);
            Assert.Equal(4 - i, free.AttemptsLeft);
        }

        Assert.Equal(TimeSpan.FromSeconds(30), (await appLock.CheckAsync("0000")).Pause);

        PinCheck paused = await appLock.CheckAsync(Pin);
        Assert.Equal(PinCheckOutcome.Paused, paused.Outcome);

        device.Pass(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromMinutes(1), (await appLock.CheckAsync("0000")).Pause);

        device.Pass(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(5), (await appLock.CheckAsync("0000")).Pause);

        Assert.Equal(TimeSpan.FromHours(1), EntryPause.After(9));
        Assert.Equal(TimeSpan.FromHours(1), EntryPause.After(100));
    }

    /// <summary>
    /// Пауза хранится вне процесса: перезапуск приложения её не снимает,
    /// а перезагрузка телефона начинает её заново целиком.
    /// </summary>
    [Fact]
    public async Task Пауза_переживает_перезапуск_и_перезагрузку()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        for (int i = 0; i < EntryPause.FreeAttempts; i++)
        {
            await appLock.CheckAsync("0000");
        }

        device.Pass(TimeSpan.FromSeconds(20));
        Assert.Equal(TimeSpan.FromSeconds(10), device.Lock().PauseRemaining());

        // После перезагрузки телефон проработал дольше, чем до неё: по одним часам
        // с включения пауза выглядела бы давно истёкшей, узнать перезагрузку — по номеру включения
        device.Reboot();
        device.Pass(TimeSpan.FromHours(2));
        Assert.Equal(TimeSpan.FromSeconds(30), device.Lock().PauseRemaining());

        // Заново начатая пауза идёт от момента, когда перезагрузку узнали, а не стоит
        device.Pass(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(20), device.Lock().PauseRemaining());

        device.Pass(TimeSpan.FromSeconds(20));
        Assert.Equal(TimeSpan.Zero, device.Lock().PauseRemaining());
    }

    [Fact]
    public async Task Верный_код_сбрасывает_счёт_неверных()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        for (int i = 0; i < EntryPause.FreeAttempts - 1; i++)
        {
            await appLock.CheckAsync("0000");
        }

        await appLock.CheckAsync(Pin);

        Assert.Equal(EntryPause.FreeAttempts - 1, (await appLock.CheckAsync("0000")).AttemptsLeft);
    }

    /// <summary>
    /// Хранилище потеряло след — сверить не с чем, и заслонка не снялась бы никогда.
    /// </summary>
    [Fact]
    public async Task Потерянный_след_выключает_защиту()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        device.Trace = null;

        Assert.Equal(PinCheckOutcome.Accepted, (await appLock.CheckAsync("0000")).Outcome);
        Assert.False(appLock.IsEnabled);

        // И заслонка снята: иначе каждый возврат из фона ставил бы её снова
        Assert.False(appLock.IsLocked);
        Assert.False(appLock.Return());
    }

    [Fact]
    public async Task Выключение_стирает_след_и_счёт()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);

        for (int i = 0; i < EntryPause.FreeAttempts; i++)
        {
            await appLock.CheckAsync("0000");
        }

        appLock.Disable();

        Assert.Null(device.Trace);
        Assert.False(appLock.IsEnabled);
        Assert.Equal(TimeSpan.Zero, appLock.PauseRemaining());
    }

    /// <summary>
    /// Экран защиты в настройках узнаёт состояние только при появлении: экран ПИН-кода
    /// меняет его, уходя, и подпись переключателя обязана догнать.
    /// </summary>
    [Fact]
    public async Task Экран_защиты_перечитывает_состояние_при_появлении()
    {
        AppLockService appLock = new TestDevice().Lock();
        AppLockViewModel screen = new(appLock);
        List<string?> changed = [];
        screen.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        screen.Load();
        Assert.True(screen.IsDisabled);

        await appLock.EnableAsync(Pin);
        screen.Load();

        Assert.True(screen.IsEnabled);
        Assert.False(screen.IsDisabled);
        Assert.Contains(nameof(AppLockViewModel.IsDisabled), changed);
    }

    /// <summary>
    /// Заслонка: неверный код встряхивает точки и стирается, с двумя оставшимися
    /// попытками называет их число, пятый гасит клавиатуру отсчётом. Верный снимает заслонку.
    /// </summary>
    [Fact]
    public async Task Заслонка_отвечает_на_набор()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);
        LockCoverViewModel cover = new(appLock);
        int shaken = 0;
        bool unlocked = false;

        cover.Rejected += (_, _) => shaken++;
        cover.Unlocked += (_, _) => unlocked = true;

        Assert.False(cover.Prepare());

        await TypeAsync(cover, "0000");
        Assert.Equal(UiTexts.PinWrong, cover.Message);
        Assert.DoesNotContain(true, cover.Dots);

        await TypeAsync(cover, "0000");
        await TypeAsync(cover, "0000");
        Assert.Equal(string.Format(UiCulture.Current, UiTexts.PinWrongAttemptsLeft, $"2 {UiTexts.PinAttemptsFew}"), cover.Message);

        await TypeAsync(cover, "0000");
        await TypeAsync(cover, "0000");
        Assert.Equal(5, shaken);
        Assert.True(cover.IsPaused);
        Assert.False(cover.CanType);
        Assert.Equal(string.Format(UiCulture.Current, UiTexts.PinPause, "0:30"), cover.Message);

        cover.Type('1');
        Assert.DoesNotContain(true, cover.Dots);

        device.Pass(TimeSpan.FromSeconds(30));
        Assert.False(cover.Tick());
        Assert.Null(cover.Message);

        await TypeAsync(cover, Pin);
        Assert.True(unlocked);
        Assert.False(appLock.IsLocked);
    }

    [Fact]
    public async Task Цифры_сверх_длины_и_стирание()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);
        LockCoverViewModel cover = new(appLock);

        cover.Prepare();

        foreach (char digit in "123456")
        {
            cover.Type(digit);
        }

        Assert.Equal([true, true, true, true], cover.Dots);

        cover.Erase();

        Assert.Equal([true, true, true, false], cover.Dots);
        Assert.False(cover.IsFull);
    }

    /// <summary>
    /// Задание кода: несовпавший повтор возвращает к новому коду, совпавший включает защиту.
    /// </summary>
    [Fact]
    public async Task Задание_кода_с_повтором()
    {
        TestDevice device = new();
        AppLockService appLock = device.Lock();
        PinViewModel screen = new(appLock);
        string? done = null;

        screen.Done += (_, text) => done = text;
        screen.Start(PinPurpose.Create);

        Assert.Equal(UiTexts.PinNewTitle, screen.Title);

        await TypeAsync(screen, "1357");
        Assert.Equal(UiTexts.PinRepeatTitle, screen.Title);

        await TypeAsync(screen, "1358");
        Assert.Equal(UiTexts.PinNewTitle, screen.Title);
        Assert.Equal(UiTexts.PinMismatch, screen.Message);
        Assert.False(appLock.IsEnabled);

        await TypeAsync(screen, "1357");
        await TypeAsync(screen, "1357");

        Assert.Equal(UiTexts.AppLockEnabledNotice, done);
        Assert.True(appLock.IsEnabled);
        Assert.Equal(PinCheckOutcome.Accepted, (await appLock.CheckAsync("1357")).Outcome);
    }

    /// <summary>
    /// Смена начинается с текущего кода, и неверный засчитывается попыткой —
    /// иначе подбирать код можно было бы здесь, мимо паузы заслонки.
    /// </summary>
    [Fact]
    public async Task Смена_кода_начинается_с_текущего()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);
        PinViewModel screen = new(appLock);
        string? done = null;

        screen.Done += (_, text) => done = text;
        screen.Start(PinPurpose.Change);

        Assert.Equal(UiTexts.PinCurrentTitle, screen.Title);

        await TypeAsync(screen, "0000");
        Assert.Equal(UiTexts.PinWrong, screen.Message);
        Assert.Equal(EntryPause.FreeAttempts - 2, (await appLock.CheckAsync("0000")).AttemptsLeft);

        await TypeAsync(screen, Pin);
        Assert.Equal(UiTexts.PinNewTitle, screen.Title);

        await TypeAsync(screen, "9753");
        await TypeAsync(screen, "9753");

        Assert.Equal(UiTexts.PinChangedNotice, done);
        Assert.Equal(PinCheckOutcome.Accepted, (await appLock.CheckAsync("9753")).Outcome);
    }

    /// <summary>
    /// Задание кода поверх включённой защиты — это смена: иначе код подменил бы
    /// тот, кто его не знает, и мимо паузы.
    /// </summary>
    [Fact]
    public async Task Задание_поверх_включённой_защиты_начинается_с_текущего()
    {
        AppLockService appLock = await UnlockedAsync(new TestDevice());
        PinViewModel screen = new(appLock);

        screen.Start(PinPurpose.Create);

        Assert.Equal(PinPurpose.Change, screen.Purpose);
        Assert.Equal(UiTexts.PinCurrentTitle, screen.Title);
    }

    [Fact]
    public async Task Выключение_после_текущего_кода()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);
        PinViewModel screen = new(appLock);
        string? done = null;

        screen.Done += (_, text) => done = text;
        screen.Start(PinPurpose.Disable);

        await TypeAsync(screen, "0000");
        Assert.True(appLock.IsEnabled);

        await TypeAsync(screen, Pin);

        Assert.Equal(UiTexts.AppLockDisabledNotice, done);
        Assert.False(appLock.IsEnabled);
        Assert.Null(device.Trace);
    }

    /// <summary>
    /// Пауза не мешает задать новый код: она про неверные попытки ввести заданный.
    /// </summary>
    [Fact]
    public async Task Пауза_не_мешает_новому_коду()
    {
        TestDevice device = new();
        AppLockService appLock = await UnlockedAsync(device);
        PinViewModel screen = new(appLock);

        for (int i = 0; i < EntryPause.FreeAttempts; i++)
        {
            await appLock.CheckAsync("0000");
        }

        Assert.True(screen.Start(PinPurpose.Change));
        Assert.True(screen.IsPaused);

        appLock.Disable();
        screen.Start(PinPurpose.Create);

        Assert.False(screen.IsPaused);
        Assert.True(screen.CanType);
    }

    [Fact]
    public async Task В_разделе_Ещё_видно_включена_ли_защита()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        MoreViewModel more = database.Resolve<MoreViewModel>();

        await more.LoadAsync();
        Assert.Equal(UiTexts.AppLockOff, more.AppLockCaption);

        await database.Resolve<AppLockService>().EnableAsync(Pin);
        await more.LoadAsync();
        Assert.Equal(UiTexts.AppLockOn, more.AppLockCaption);
    }

    /// <summary>
    /// Предложение — один раз и только на новой базе: обновившему приложение
    /// и восстановившему выгрузку оно не всплывает.
    /// </summary>
    [Fact]
    public async Task Предложение_только_при_первом_запуске()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();

        await database.Resolve<FinanceStartup>().PrepareAsync();

        AppLockOffer offer = database.Resolve<AppLockOffer>();

        Assert.True(offer.Take());
        Assert.False(offer.Take());
    }

    [Fact]
    public async Task Предложения_нет_на_стоящей_базе()
    {
        await using TestDatabase database = await TestDatabase.CreateWithPresetAsync();

        await database.Resolve<FinanceStartup>().PrepareAsync();

        Assert.False(database.Resolve<AppLockOffer>().Take());
    }

    [Fact]
    public async Task Предложения_нет_при_включённой_защите()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();

        await database.Resolve<FinanceStartup>().PrepareAsync();
        await database.Resolve<AppLockService>().EnableAsync(Pin);

        Assert.False(database.Resolve<AppLockOffer>().Take());
    }

    private static async Task<AppLockService> UnlockedAsync(TestDevice device)
    {
        AppLockService appLock = device.Lock();

        await appLock.EnableAsync(Pin);

        appLock.Start();
        await appLock.CheckAsync(Pin);

        return appLock;
    }

    private static async Task TypeAsync(PinPadViewModel pad, string pin)
    {
        foreach (char digit in pin)
        {
            pad.Type(digit);
        }

        await pad.SubmitAsync();
    }
}

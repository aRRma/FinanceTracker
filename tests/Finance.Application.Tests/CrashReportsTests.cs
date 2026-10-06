using System.Globalization;
using Finance.Application.Infrastructure.Diagnostics;
using Finance.Application.Infrastructure.Storage;
using Finance.Domain.Enums;
using Finance.Domain.Errors;
using Finance.Domain.Values;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Application.Tests;

/// <summary>
/// Отчёты о сбоях в файле: что пишется, чего в отчёт не попадает, смена файла и чтение после оборванной записи.
/// </summary>
public sealed class CrashReportsTests : IDisposable
{
    private static readonly DeviceInfo Device = new() { AppVersion = "1.0.7", AppBuild = "10007", Android = "16", Model = "Google Pixel 7" };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "finance-crash-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Отчёт читается тем, чем записан: момент, вид, тип исключения строкой списка, устройство и стек в тексте.
    /// </summary>
    [Fact]
    public async Task Отчёт_читается_тем_же_что_записан()
    {
        CrashReports reports = Create();

        Assert.True(reports.Write(CrashKind.Unhandled, Thrown(static () => new InvalidOperationException("что-то"))));

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Equal(TestTime.Start.AddMilliseconds(1), report.AtUtc);
        Assert.Equal(CrashKind.Unhandled, report.Kind);
        Assert.Equal("System.InvalidOperationException", report.Headline);
        Assert.Contains("1.0.7 (10007), android 16, Google Pixel 7", report.Text, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Текст исключения вне белого списка в отчёт не попадает: в нём бывает набранная сумма
    /// или имя группы, а отчёт уходит из телефона.
    /// </summary>
    [Fact]
    public async Task Текст_исключения_вне_списка_не_пишется()
    {
        CrashReports reports = Create();

        reports.Write(CrashKind.Caught, Thrown(static () => new FormatException("1 234,56 Пятёрочка")));

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Equal("System.FormatException", report.Headline);
        Assert.DoesNotContain("1 234,56", report.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Пятёрочка", report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Сорванная миграция пишется со своим текстом и с вложенным исключением, а вложенное — по тому же
    /// правилу: тип есть, текста нет.
    /// </summary>
    [Fact]
    public async Task Вложенное_исключение_пишется_по_тому_же_правилу()
    {
        CrashReports reports = Create();
        Exception inner = Thrown(static () => new InvalidOperationException("Группа «Пятёрочка» не найдена"));

        reports.Write(CrashKind.Caught, new DatabaseMigrationException("Миграция не удалась", inner));

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Contains("DatabaseMigrationException: Миграция не удалась", report.Text, StringComparison.Ordinal);
        Assert.Contains("--- inner 1: System.InvalidOperationException", report.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Пятёрочка", report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Все исключения составного пишутся, а не только первое.
    /// </summary>
    [Fact]
    public async Task У_составного_исключения_пишутся_все_вложенные()
    {
        CrashReports reports = Create();

        reports.Write(CrashKind.Warning, new AggregateException(new TimeoutException(), new InvalidCastException()));

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Contains("--- inner 1: System.TimeoutException", report.Text, StringComparison.Ordinal);
        Assert.Contains("--- inner 1: System.InvalidCastException", report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Цепочка вложенных разворачивается до предела глубины, а не до конца: сотня обёрток ничего не добавит к первым.
    /// </summary>
    [Fact]
    public async Task Вложенные_разворачиваются_до_предела_глубины()
    {
        CrashReports reports = Create();
        Exception chain = new TimeoutException();

        for (int level = 0; level < 12; level++)
        {
            chain = new InvalidOperationException(null, chain);
        }

        reports.Write(CrashKind.Caught, chain);

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Contains("--- inner 8: ", report.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("--- inner 9: ", report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Отчёты, созданные платформой до сборки служб, — те же, что достаются экрану: файл и блокировка общие.
    /// </summary>
    [Fact]
    public void Переданные_отчёты_регистрируются_как_есть()
    {
        CrashReports reports = Create();

        using ServiceProvider services = new ServiceCollection()
            .AddFinance(Path.Combine(_folder, "finance.db"), crashReports: reports)
            .BuildServiceProvider();

        Assert.Same(reports, services.GetRequiredService<CrashReports>());
    }

    /// <summary>
    /// Нарушенное правило и отмена — не сбои: отчёта нет.
    /// </summary>
    [Fact]
    public async Task Нарушенное_правило_и_отмена_не_пишутся()
    {
        CrashReports reports = Create();
        DomainException rule = Assert.Throws<DomainException>(static () => Money.Create(1.505m, Currency.RUB));

        Assert.False(reports.Write(CrashKind.Caught, rule));
        Assert.False(reports.Write(CrashKind.Caught, new OperationCanceledException()));

        Assert.Empty(await reports.ReadAsync(CancellationToken.None));
    }

    /// <summary>
    /// Одно падение проходит через несколько обработчиков подряд, а отчёт о нём один — от первого,
    /// в том числе когда обработчики срабатывают из разных потоков разом. Пойманные сбои это не задевает.
    /// </summary>
    [Fact]
    public async Task Падение_даёт_один_отчёт_от_первого_обработчика()
    {
        CrashReports reports = Create();

        bool[] written = await Task.WhenAll(
            Enumerable.Range(0, 3).Select(_ => Task.Run(() => reports.WriteFatal(CrashKind.Unhandled, new TimeoutException()))));
        Assert.Single(written, static first => first);

        Assert.False(reports.WriteFatal(CrashKind.Java, new TimeoutException()));
        Assert.True(reports.Write(CrashKind.Caught, new TimeoutException()));

        IReadOnlyList<CrashReport> read = await reports.ReadAsync(CancellationToken.None);
        Assert.Equal([CrashKind.Caught, CrashKind.Unhandled], read.Select(static report => report.Kind));
    }

    /// <summary>
    /// Необработанное нарушение правила роняет приложение и потому пишется — но без своего текста, где бывают имена.
    /// </summary>
    [Fact]
    public async Task Падение_на_нарушенном_правиле_пишется_без_текста()
    {
        CrashReports reports = Create();
        DomainException rule = Assert.Throws<DomainException>(static () => Money.Create(1.505m, Currency.RUB));

        Assert.True(reports.WriteFatal(CrashKind.Unhandled, rule));

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Equal(typeof(DomainException).FullName, report.Headline);
    }

    /// <summary>
    /// Падение внутри Java пишется по стеку Java: вызовы остаются, сообщения — нет, в том числе у вложенных
    /// и многострочные. В сообщении обёртки над исключением .NET лежит его текст.
    /// </summary>
    [Fact]
    public async Task Стек_Java_пишется_без_сообщений()
    {
        CrashReports reports = Create();
        const string stack = """
            java.lang.RuntimeException: Unable to start activity: 1 234,56 Пятёрочка
            вторая строка сообщения
            	at android.app.ActivityThread.performLaunchActivity(ActivityThread.java:4280)
            	at android.os.Looper.loop(Looper.java:338)
            Caused by: android.runtime.JavaProxyThrowable: [System.FormatException]: Пятёрочка
            	at crc64.MainActivity.n_onCreate(Native Method)
            	... 12 more
            Suppressed: java.io.IOException: Пятёрочка
            """;

        Assert.True(reports.WriteFatalJava(stack));
        Assert.False(reports.WriteFatal(CrashKind.Unhandled, new TimeoutException()));

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Equal(CrashKind.Java, report.Kind);
        Assert.Equal("java.lang.RuntimeException", report.Headline);
        Assert.Contains("\tat android.os.Looper.loop(Looper.java:338)", report.Text, StringComparison.Ordinal);
        Assert.Contains("Caused by: android.runtime.JavaProxyThrowable\n", report.Text, StringComparison.Ordinal);
        Assert.Contains("\t... 12 more", report.Text, StringComparison.Ordinal);
        Assert.Contains("Suppressed: java.io.IOException", report.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Пятёрочка", report.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("1 234,56", report.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("вторая строка", report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Новые отчёты читаются первыми.
    /// </summary>
    [Fact]
    public async Task Новые_отчёты_читаются_первыми()
    {
        CrashReports reports = Create();

        reports.Write(CrashKind.Caught, new TimeoutException());
        reports.Write(CrashKind.Java, new TimeoutException());
        reports.Write(CrashKind.Warning, new TimeoutException());

        IReadOnlyList<CrashReport> read = await reports.ReadAsync(CancellationToken.None);
        Assert.Equal([CrashKind.Warning, CrashKind.Java, CrashKind.Caught], read.Select(static report => report.Kind));
    }

    /// <summary>
    /// Текущий файл, дорастая до предела, уезжает в предыдущий: файлов два, ни один не больше предела,
    /// и читаются последние отчёты подряд, без пропусков.
    /// </summary>
    [Fact]
    public async Task Файл_сменяется_на_пределе_и_последние_отчёты_целы()
    {
        CrashReports reports = Create(fileLimit: 2048);

        for (int number = 1; number <= 40; number++)
        {
            Assert.True(reports.Write(CrashKind.Caught, Numbered(number)));
        }

        Assert.True(new FileInfo(reports.CurrentPath).Length <= 2048);
        Assert.True(new FileInfo(reports.PreviousPath).Length <= 2048);

        int[] numbers = Numbers(await reports.ReadAsync(CancellationToken.None));
        Assert.True(numbers.Length > 2);
        Assert.Equal(40, numbers[0]);
        Assert.Equal(Enumerable.Range(40 - numbers.Length + 1, numbers.Length).Reverse(), numbers);
    }

    /// <summary>
    /// Отчёты из разных потоков разом, в том числе на смене файла, не теряются и не перемешиваются.
    /// </summary>
    [Fact]
    public async Task Одновременные_отчёты_не_перемешиваются()
    {
        CrashReports whole = Create();
        CrashReports rotating = Create(fileLimit: 4096, subfolder: "rotating");

        await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() =>
        {
            for (int index = 0; index < 50; index++)
            {
                int number = (thread * 50) + index + 1;
                whole.Write(CrashKind.Unhandled, Numbered(number));
                rotating.Write(CrashKind.Unhandled, Numbered(number));
            }
        })));

        Assert.Equal(Enumerable.Range(1, 400), Numbers(await whole.ReadAsync(CancellationToken.None)).Order());

        // При смене файла часть старых уходит, но каждый оставшийся отчёт цел, и все разные
        int[] kept = Numbers(await rotating.ReadAsync(CancellationToken.None));
        Assert.NotEmpty(kept);
        Assert.Equal(kept.Length, kept.Distinct().Count());
        Assert.Equal(kept.Length, CountEnds(rotating));
    }

    /// <summary>
    /// Оборванная запись — процесс умер посреди неё — не мешает читать остальные: ни в конце файла, ни в середине.
    /// </summary>
    [Fact]
    public async Task Оборванный_отчёт_не_мешает_читать_остальные()
    {
        CrashReports reports = Create();

        reports.Write(CrashKind.Caught, Numbered(1));
        File.AppendAllText(reports.CurrentPath, "=== 2026-09-15T12:00:00.0000000+00:00 Unhandled\napp 1.0.7\nSystem.Invalid");
        reports.Write(CrashKind.Caught, Numbered(2));
        File.AppendAllText(reports.CurrentPath, "=== 2026-09-15T12:00:00.0000000+00:00 Java\napp");

        Assert.Equal(new[] { 2, 1 }, Numbers(await reports.ReadAsync(CancellationToken.None)));
    }

    /// <summary>
    /// Строка сбоя, похожая на служебную, не закрывает отчёт и не открывает новый.
    /// </summary>
    [Fact]
    public async Task Строка_похожая_на_служебную_не_ломает_отчёт()
    {
        CrashReports reports = Create();

        reports.Write(CrashKind.Caught, new DatabaseMigrationException("a\n=== end\n=== 2026-09-15T12:00:00.0000000+00:00 Java\nb"));

        CrashReport report = Assert.Single(await reports.ReadAsync(CancellationToken.None));
        Assert.Equal(CrashKind.Caught, report.Kind);
        Assert.Contains("\nb", report.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Очистка удаляет оба файла, и следующий отчёт пишется заново.
    /// </summary>
    [Fact]
    public async Task Очистка_удаляет_всё()
    {
        CrashReports reports = Create(fileLimit: 1024);

        for (int number = 1; number <= 10; number++)
        {
            reports.Write(CrashKind.Caught, Numbered(number));
        }

        reports.Clear();

        Assert.Empty(await reports.ReadAsync(CancellationToken.None));

        reports.Write(CrashKind.Caught, Numbered(11));
        Assert.Equal(new[] { 11 }, Numbers(await reports.ReadAsync(CancellationToken.None)));
    }

    /// <summary>
    /// Не записалось — наружу ничего не бросается: исключение из записи потеряло бы исходный сбой.
    /// </summary>
    [Fact]
    public void Сбой_записи_не_выходит_наружу()
    {
        // Папку отчётов не создать: на её месте файл
        File.WriteAllText(_folder, "");
        CrashReports reports = Create();

        Assert.False(reports.Write(CrashKind.Unhandled, new TimeoutException()));
    }

    /// <summary>
    /// Неудачная запись первого обработчика падения не глушит следующих: признак «уже записано» ставит только записанный отчёт.
    /// </summary>
    [Fact]
    public async Task Неудачная_запись_падения_не_глушит_следующий_обработчик()
    {
        File.WriteAllText(_folder, "");
        CrashReports reports = Create();

        Assert.False(reports.WriteFatal(CrashKind.Unhandled, new TimeoutException()));

        File.Delete(_folder);

        Assert.True(reports.WriteFatalJava("java.lang.IllegalStateException: x\n\tat a.b(C.java:1)"));
        Assert.Equal(CrashKind.Java, Assert.Single(await reports.ReadAsync(CancellationToken.None)).Kind);
    }

    /// <summary>
    /// Исключение, у которого бросает само чтение стека или текста, не выпускает наружу ничего:
    /// бросок из обработчика сбоя заменил бы исходный сбой.
    /// </summary>
    [Fact]
    public void Бросающее_описание_не_выходит_наружу()
    {
        CrashReports reports = Create();

        Assert.False(reports.Write(CrashKind.Caught, new BrokenException()));
        Assert.False(reports.WriteFatal(CrashKind.Unhandled, new BrokenException()));
    }

    /// <summary>
    /// Чтение во время записи со сменой файла видит последние отчёты подряд: смена файла между чтением
    /// двух файлов теряла бы те, что уехали из текущего в предыдущий.
    /// </summary>
    [Fact]
    public async Task Чтение_во_время_смены_файла_не_теряет_отчётов()
    {
        CrashReports reports = Create(fileLimit: 1024);

        Task writer = Task.Run(() =>
        {
            for (int number = 1; number <= 600; number++)
            {
                reports.Write(CrashKind.Caught, Numbered(number));
            }
        });

        while (!writer.IsCompleted)
        {
            int[] numbers = Numbers(await reports.ReadAsync(CancellationToken.None));

            if (numbers.Length > 0)
            {
                Assert.Equal(Enumerable.Range(numbers[^1], numbers.Length).Reverse(), numbers);
            }
        }

        await writer;
    }

    /// <summary>
    /// Без переданных отчётов прикладной слой заводит их сам рядом с базой.
    /// </summary>
    [Fact]
    public async Task Отчёты_заводятся_рядом_с_базой()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        CrashReports reports = database.Resolve<CrashReports>();

        Assert.Equal(Path.GetDirectoryName(database.Location.Path), Path.GetDirectoryName(reports.CurrentPath));
        Assert.Same(reports, database.Resolve<CrashReports>());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
        else if (File.Exists(_folder))
        {
            File.Delete(_folder);
        }
    }

    private CrashReports Create(int fileLimit = CrashReports.DefaultFileLimit, string subfolder = "") =>
        new(Path.Combine(_folder, subfolder), new TestTime(), Device, fileLimit);

    /// <summary>
    /// Исключение, брошенное по-настоящему: у сконструированного стека нет.
    /// </summary>
    private static Exception Thrown(Func<Exception> make) => Assert.ThrowsAny<Exception>(new Action(() => throw make()));

    /// <summary>
    /// Отчёт с номером в тексте: у сорванной миграции текст пишется, и по номеру видно, какие отчёты дошли.
    /// </summary>
    private static DatabaseMigrationException Numbered(int number) =>
        new(number.ToString(CultureInfo.InvariantCulture));

    private static int[] Numbers(IEnumerable<CrashReport> reports) =>
        [.. reports.Select(static report => int.Parse(report.Headline.Split(": ")[1], CultureInfo.InvariantCulture))];

    /// <summary>
    /// Исключение моста Java в развалившейся среде: и текст, и стек бросают при чтении.
    /// </summary>
    private sealed class BrokenException : Exception
    {
        public override string Message => throw new InvalidOperationException("message");

        public override string StackTrace => throw new InvalidOperationException("stack");
    }

    private static int CountEnds(CrashReports reports) =>
        new[] { reports.CurrentPath, reports.PreviousPath }
            .Where(File.Exists)
            .Sum(static path => File.ReadAllLines(path).Count(static line => line == "=== end"));
}

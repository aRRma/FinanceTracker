using System.Text;
using Finance.Domain.Errors;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// Отчёты о сбоях в файле на телефоне. Вне базы: в выгрузку не попадают, восстановление их не трогает,
/// а записать отчёт можно и тогда, когда сбой случился в самой базе.
/// </summary>
/// <remarks>
/// Создаётся раньше контейнера служб: перехват сбоев подключается до сборки приложения, иначе сбой при старте
/// не попал бы никуда. Поэтому момент берётся у <see cref="TimeProvider"/>, а не у <see cref="IClock"/> —
/// часы приложения читают тот же источник, но живут в контейнере. Файлов два: текущий до предела размера
/// и один предыдущий, так что место на телефоне отчёты не съедают.
/// </remarks>
public sealed class CrashReports
{
    /// <summary>
    /// Предел текущего файла. Отчёт — около килобайта, так что в двух файлах помещаются сотни.
    /// </summary>
    public const int DefaultFileLimit = 256 * 1024;

    private const string CurrentName = "crash-reports.txt";
    private const string PreviousName = "crash-reports.previous.txt";

    private readonly Lock _gate = new();
    private readonly string _folder;
    private readonly TimeProvider _time;
    private readonly DeviceInfo _device;
    private readonly int _fileLimit;
    private bool _fatalWritten;

    /// <summary>
    /// Отчёты в папке данных приложения.
    /// </summary>
    /// <param name="folder">Папка данных приложения.</param>
    /// <param name="time">Источник момента.</param>
    /// <param name="device">Сборка и устройство.</param>
    public CrashReports(string folder, TimeProvider time, DeviceInfo device)
        : this(folder, time, device, DefaultFileLimit)
    {
    }

    /// <summary>
    /// Отчёты с заданным пределом файла — тестам, чтобы дойти до смены файла за десяток отчётов, а не за сотни.
    /// </summary>
    /// <param name="folder">Папка данных приложения.</param>
    /// <param name="time">Источник момента.</param>
    /// <param name="device">Сборка и устройство.</param>
    /// <param name="fileLimit">Предел текущего файла в байтах.</param>
    internal CrashReports(string folder, TimeProvider time, DeviceInfo device, int fileLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileLimit);

        _folder = folder;
        _time = time;
        _device = device;
        _fileLimit = fileLimit;
        Trail = new ActionTrail(time);
    }

    /// <summary>
    /// След действий: выписывается в каждый отчёт. Создаётся вместе с отчётами — так же раньше контейнера служб.
    /// </summary>
    public ActionTrail Trail { get; }

    /// <summary>
    /// Текущий файл — его отдаёт «Поделиться».
    /// </summary>
    public string CurrentPath => Path.Combine(_folder, CurrentName);

    /// <summary>
    /// Предыдущий файл: сюда уезжает текущий, когда дорастает до предела.
    /// </summary>
    public string PreviousPath => Path.Combine(_folder, PreviousName);

    /// <summary>
    /// Пишет отчёт о сбое. Нарушенное правило и отмена сбоями не считаются и не пишутся.
    /// </summary>
    /// <param name="kind">Откуда пришёл сбой.</param>
    /// <param name="error">Сбой.</param>
    /// <returns><c>true</c>, если отчёт записан.</returns>
    /// <remarks>
    /// Синхронно: при падении процесс умирает сразу за обработчиком, и асинхронная запись не успела бы.
    /// Сбой записи наружу не бросает — он потерял бы исходный сбой, ради которого она шла.
    /// </remarks>
    public bool Write(CrashKind kind, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        // Пойманное нарушенное правило объяснено пользователю его же текстом, а отмена — не сбой.
        // Обёрнутые в составное исключение забытой задачи пишутся: их не видел никто
        if (error is DomainException or OperationCanceledException)
        {
            return false;
        }

        return Record(kind, () => CrashDescription.Describe(error));
    }

    /// <summary>
    /// Пишет сбой, после которого процесс закрывается. Такой отчёт один на процесс: следующие вызовы молчат.
    /// </summary>
    /// <param name="kind">Какой обработчик поймал сбой первым.</param>
    /// <param name="error">Сбой.</param>
    /// <returns><c>true</c>, если отчёт записан этим вызовом.</returns>
    /// <remarks>
    /// Одно падение проходит через несколько обработчиков подряд: исключение .NET на главном потоке видят
    /// перехват среды Android, обработчик Java и <see cref="AppDomain.UnhandledException"/>. Пишет первый,
    /// и вид отчёта — его. Не записалось — следующий обработчик того же падения пробует сам. Нарушенное
    /// правило и отмена здесь тоже пишутся: необработанными они роняют приложение, а текст правила в отчёт
    /// всё равно не попадает.
    /// </remarks>
    public bool WriteFatal(CrashKind kind, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Fatal(kind, () => CrashDescription.Describe(error));
    }

    /// <summary>
    /// Пишет падение внутри Java по его стеку. Как и <see cref="WriteFatal(CrashKind, Exception)"/>, один отчёт на процесс.
    /// </summary>
    /// <param name="stack">Стек Java целиком, как его печатает Java.</param>
    /// <returns><c>true</c>, если отчёт записан этим вызовом.</returns>
    /// <remarks>
    /// Обёртка .NET над исключением Java знает только свою, управляемую часть стека — для падения внутри
    /// Java она пуста. Поэтому стек приходит текстом, а сообщения из него вырезаются.
    /// </remarks>
    public bool WriteFatalJava(string stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        return Fatal(CrashKind.Java, () => CrashDescription.DescribeJava(stack));
    }

    /// <summary>
    /// Пишет отчёт о прошлом завершении процесса — с моментом самого завершения и без следа: след у этого
    /// процесса свой, к прошлому он отношения не имеет.
    /// </summary>
    /// <param name="atUtc">Когда прошлый процесс завершился.</param>
    /// <param name="description">Описание, собранное из записи системы.</param>
    /// <returns><c>true</c>, если отчёт записан.</returns>
    internal bool WriteExit(DateTimeOffset atUtc, string description) =>
        Record(CrashKind.ExitReason, atUtc, () => description, []);

    /// <summary>
    /// Читает отчёты обоих файлов, от новых к старым по моменту сбоя.
    /// </summary>
    /// <param name="cancellationToken">Отмена до начала чтения.</param>
    /// <remarks>
    /// Оба файла читаются разом под той же блокировкой, что запись: смена файла между двумя чтениями
    /// потеряла бы отчёты, уехавшие из текущего в предыдущий. Поэтому чтение синхронное — блокировку
    /// не держат через <c>await</c>, — и уходит в пул потоков: файлы не больше полумегабайта.
    /// </remarks>
    public Task<IReadOnlyList<CrashReport>> ReadAsync(CancellationToken cancellationToken) =>
        Task.Run(Read, cancellationToken);

    /// <summary>
    /// Сводит оба файла в один — для «Поделиться»: отправить файлом удобнее, чем двумя.
    /// </summary>
    /// <param name="path">Куда положить сведённый файл.</param>
    /// <remarks>
    /// Под той же блокировкой, что запись: смена файла посреди копирования потеряла бы отчёты. Предыдущий
    /// файл может кончаться оборванной записью — без перевода строки первый отчёт текущего склеился бы с ней.
    /// </remarks>
    public void CopyTo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (_gate)
        {
            string previous = ReadText(PreviousPath);
            string joint = previous.Length is 0 || previous.EndsWith('\n') ? "" : "\n";

            File.WriteAllText(path, previous + joint + ReadText(CurrentPath), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    /// <summary>
    /// Кладёт один отчёт в отдельный файл — для «Поделиться» с экрана отчёта.
    /// </summary>
    /// <param name="report">Отчёт.</param>
    /// <param name="path">Куда положить файл.</param>
    public static void CopyTo(CrashReport report, string path)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        File.WriteAllText(path, CrashReportFile.Format(report), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Удаляет все отчёты.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            File.Delete(CurrentPath);
            File.Delete(PreviousPath);
        }
    }

    private bool Fatal(CrashKind kind, Func<string> describe)
    {
        // Под блокировкой записи: обработчик того же падения в другом потоке ждёт, пока первый допишет, —
        // иначе он отдал бы падение дальше, и процесс умер бы посреди отчёта. Признак ставится только
        // удачной записью: неудачная не глушит следующий обработчик
        lock (_gate)
        {
            if (_fatalWritten)
            {
                return false;
            }

            _fatalWritten = Record(kind, describe);

            return _fatalWritten;
        }
    }

    private bool Record(CrashKind kind, Func<string> describe) =>
        Record(kind, _time.GetUtcNow(), describe, Trail.Snapshot());

    private bool Record(CrashKind kind, DateTimeOffset atUtc, Func<string> describe, IReadOnlyList<string> trail)
    {
        try
        {
            byte[] report = Encoding.UTF8.GetBytes(CrashReportFile.Format(atUtc, kind, _device, describe(), trail));

            // Сбой потока и сбой действия приходят одновременно: без блокировки два отчёта
            // перемешались бы, а смена файла потеряла бы один из них
            lock (_gate)
            {
                Append(report);
            }

            return true;
        }
        // Обработчик сбоя не бросает ничего: исключение отсюда заменило бы исходный сбой. Бросить может
        // не только запись — текст и стек исключения из Java читаются через среду, которая в эту минуту
        // уже разваливается
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            return false;
        }
    }

    private void Append(byte[] report)
    {
        Directory.CreateDirectory(_folder);

        FileInfo current = new(CurrentPath);

        if (current.Exists && current.Length + report.Length > _fileLimit)
        {
            File.Move(CurrentPath, PreviousPath, overwrite: true);
        }

        using FileStream stream = new(CurrentPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        // Прошлая запись оборвалась посреди строки: без перевода строки заголовок этого
        // отчёта склеился бы с её хвостом, и пропал бы уже он
        if (stream.Length > 0)
        {
            stream.Seek(-1, SeekOrigin.End);

            if (stream.ReadByte() is not '\n')
            {
                stream.WriteByte((byte)'\n');
            }
        }

        stream.Seek(0, SeekOrigin.End);
        stream.Write(report);
        stream.Flush(flushToDisk: true);
    }

    private IReadOnlyList<CrashReport> Read()
    {
        string previous;
        string current;

        lock (_gate)
        {
            previous = ReadText(PreviousPath);
            current = ReadText(CurrentPath);
        }

        List<CrashReport> reports = [.. CrashReportFile.Parse(previous), .. CrashReportFile.Parse(current)];
        reports.Reverse();

        // По моменту сбоя, а не по порядку записи: отчёт о прошлом завершении пишется при следующем
        // запуске, но с моментом самого завершения и встаёт среди отчётов по нему. Сортировка
        // устойчивая — при равных моментах позже записанный остаётся выше
        return [.. reports.OrderByDescending(static report => report.AtUtc)];
    }

    private static string ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException)
        {
            return "";
        }
    }
}

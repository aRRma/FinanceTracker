#!/usr/bin/env dotnet
#:package Microsoft.Data.Sqlite@10.0.12
#:property PublishAot=false

// Помощник проверки на эмуляторе: экран текстом вместо снимка, нажатие по id или подписи,
// ожидание вместо sleep, запрос к базе приложения, падения из журнала.
// Команды идут цепочкой в одном запуске: dotnet tools/ui.cs -- tap AddAccount wait "Новый счёт" dump
// Справка: dotnet tools/ui.cs -- help

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length is 0 || args[0] is "help" or "-h" or "--help")
{
    Console.WriteLine(Ui.Help);
    return 0;
}

try
{
    Ui ui = new();
    Queue<string> queue = new(args);

    while (queue.Count > 0)
    {
        string command = queue.Dequeue();
        await ui.RunAsync(command, queue);
    }

    return 0;
}
catch (UiException error)
{
    Console.Error.WriteLine($"ОШИБКА: {error.Message}");
    return 1;
}

/// <summary>
/// Сбой команды с понятным текстом: печатается без стека, и цепочка команд на нём обрывается.
/// </summary>
internal sealed class UiException(string message) : Exception(message);

/// <summary>
/// Узел выгрузки экрана, сжатый до того, что нужно для чтения и нажатия.
/// </summary>
/// <remarks>
/// Нажимаемая строка без своей подписи забирает подписи вложенных узлов в <see cref="Parts"/>:
/// строка списка в MAUI — это пустой контейнер с касанием и надписи внутри него.
/// </remarks>
internal sealed record UiNode(string Id, IReadOnlyList<string> Parts, string Hint, int X, int Y, string Flags)
{
    public string Text => string.Join(" · ", Parts);

    public string Label => Parts.Count > 0 ? Text : Hint;
}

/// <summary>
/// Команды помощника. Всё идёт через adb напрямую, без оболочки: пути устройства
/// вроде /sdcard не проходят через Git Bash, который переписал бы их в пути Windows.
/// </summary>
internal sealed partial class Ui
{
    private const string Package = "ru.finance.tracker";
    private const string Activity = Package + "/" + Package + ".MainActivity";
    private const string ShortcutAction = Package + ".action.ADD_TRANSACTION";
    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(20);

    public const string Help = """
        dotnet tools/ui.cs -- <команда> [аргументы] [<команда> ...]   команды выполняются по порядку

          boot [avd]           запустить эмулятор, если не запущен, дождаться загрузки, выключить анимации,
                               выставить часовой пояс машины; с именем AVD — именно его, чужой гасится
          run                  собрать и развернуть приложение (-t:Run), очистив журнал, и дождаться его окна
          start                холодный старт: force-stop и запуск с ожиданием первого кадра
          stop                 force-stop приложения
          shortcut <вид>       открыть приложение ярлыком: Expense | Income | Transfer (после force-stop)
          debugcrash <вид>     нарочный сбой отладочной сборки: caught | ui | thread | timer | task | java | anr
          dump                 экран списком: * нажимаемый, x,y центра, id, подпись, [состояния]
          tap <цель>           нажать: AutomationId, точная подпись, часть подписи или x,y
          tap2 <цель>          два нажатия подряд (проверка защиты от повторного сохранения)
          hold <цель>          долгое нажатие, 600 мс
          wait <цель>          ждать появления цели (до 20 с)
          gone <цель>          ждать исчезновения цели (до 20 с)
          text <строка>        набрать текст в поле с фокусом; только ASCII
          key <клавиша>        back | enter | del | hide | home
          swipe <куда>         up | down — прокрутка на полэкрана
          db <sql>             запрос к базе приложения (копия файлов базы, WAL учтён)
          crash                падения из журнала: Java и .NET
          nocrash              то же как проверка: есть падение — ошибка, цепочка обрывается
          shot [файл]          снимок экрана, по умолчанию artifacts/shot.png — только для вёрстки
          edges                нижние края прокручиваемых областей и кнопок Add*/Save* — запоминаются
          same                 края снова и сверка с последним edges: сдвиг — ошибка (устаревший отступ снизу)
          transit <цель|back>  переход под замедленной вдесятеро анимацией: кадры подряд, полоса под строкой
                               состояния не должна чернеть; анимации после выключаются
          script <файл>        цепочка из файла: строка — шаг из одной или нескольких команд, # — комментарий,
                               аргумент с пробелами — в двойных кавычках; сбой называет номер строки
        """;

    private readonly string _adb = Sdk("platform-tools", "adb.exe");

    private string? _edges;

    public async Task RunAsync(string command, Queue<string> queue)
    {
        switch (command)
        {
            case "boot": await BootAsync(queue.Count > 0 && !IsCommand(queue.Peek()) ? queue.Dequeue() : null); break;
            case "run": await DeployAsync(); break;
            case "start": await StartAsync(); break;
            case "stop": await AdbAsync("shell", "am", "force-stop", Package); break;
            case "shortcut": await ShortcutAsync(Take(queue, command)); break;
            case "debugcrash": await DebugCrashAsync(Take(queue, command)); break;
            case "dump": Print(await DumpAsync()); break;
            case "tap": await TapAsync(Take(queue, command), times: 1); break;
            case "tap2": await TapAsync(Take(queue, command), times: 2); break;
            case "hold": await HoldAsync(Take(queue, command)); break;
            case "wait": await WaitAsync(Take(queue, command), present: true); break;
            case "gone": await WaitAsync(Take(queue, command), present: false); break;
            case "text": await TypeAsync(Take(queue, command)); break;
            case "key": await KeyAsync(Take(queue, command)); break;
            case "swipe": await SwipeAsync(Take(queue, command)); break;
            case "db": await QueryAsync(Take(queue, command)); break;
            case "crash": await CrashAsync(); break;
            case "nocrash": await NoCrashAsync(); break;
            case "shot": await ShotAsync(queue.Count > 0 && !IsCommand(queue.Peek()) ? queue.Dequeue() : "artifacts/shot.png"); break;
            case "edges": _edges = await EdgesAsync(); Console.WriteLine($"края: {_edges}"); break;
            case "same": await SameEdgesAsync(); break;
            case "transit": await TransitAsync(Take(queue, command)); break;
            case "script": await ScriptAsync(Take(queue, command)); break;
            default: throw new UiException($"неизвестная команда «{command}». Справка: dotnet tools/ui.cs -- help");
        }
    }

    private static bool IsCommand(string word) =>
        word is "boot" or "run" or "start" or "stop" or "shortcut" or "debugcrash" or "dump" or "tap" or "tap2" or "hold"
            or "wait" or "gone" or "text" or "key" or "swipe" or "db" or "crash" or "nocrash" or "shot" or "edges" or "same"
            or "transit" or "script";

    private static string Take(Queue<string> queue, string command) =>
        queue.Count > 0 ? queue.Dequeue() : throw new UiException($"команде {command} нужен аргумент");

    /// <summary>
    /// Сценарий из файла. Строка идёт отдельной цепочкой со своим номером в выводе:
    /// упавший шаг длинного сценария иначе пришлось бы искать по выводу экрана.
    /// Запомненные края (<c>edges</c>) живут через строки — <c>same</c> сверяется с ними.
    /// </summary>
    private async Task ScriptAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new UiException($"нет файла сценария {path}");
        }

        string[] lines = await File.ReadAllLinesAsync(path);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Length is 0 || line.StartsWith('#'))
            {
                continue;
            }

            Console.WriteLine($"[{Path.GetFileName(path)}:{i + 1}] {line}");

            try
            {
                // Разбор строки — внутри try: незакрытая кавычка тоже называет номер строки
                Queue<string> steps = new(Words(line));

                while (steps.Count > 0)
                {
                    await RunAsync(steps.Dequeue(), steps);
                }
            }
            catch (UiException error)
            {
                throw new UiException($"{Path.GetFileName(path)}, строка {i + 1}: {error.Message}");
            }
        }
    }

    /// <summary>
    /// Слова строки сценария: пробел делит, двойные кавычки держат вместе — так пишется
    /// подпись из нескольких слов и запрос к базе.
    /// </summary>
    private static List<string> Words(string line)
    {
        List<string> words = [];
        StringBuilder word = new();
        bool quoted = false;
        bool started = false;

        foreach (char symbol in line)
        {
            if (symbol is '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (char.IsWhiteSpace(symbol) && !quoted)
            {
                if (started)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    started = false;
                }
            }
            else
            {
                word.Append(symbol);
                started = true;
            }
        }

        if (quoted)
        {
            throw new UiException($"незакрытая кавычка: {line}");
        }

        if (started)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>
    /// Поднимает эмулятор. Без имени — первый AVD из вывода <c>emulator -list-avds</c>, если ни один не запущен.
    /// С именем — именно этот: запущенный чужой гасится. Иначе проверка первого запуска
    /// молча уходила на эмулятор с данными, где счёт подставляется сам.
    /// </summary>
    private async Task BootAsync(string? wanted)
    {
        string emulator = Sdk("emulator", "emulator.exe");
        string[] avds = (await RunAsync(emulator, ["-list-avds"]))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (wanted is not null && !avds.Contains(wanted))
        {
            throw new UiException($"нет AVD «{wanted}». Есть: {string.Join(", ", avds)}");
        }

        bool running = await IsDeviceAsync();

        if (running && wanted is not null && await RunningAvdAsync() is { } current && current != wanted)
        {
            await AdbAsync("emu", "kill");

            Stopwatch stopping = Stopwatch.StartNew();
            while (await IsDeviceAsync())
            {
                Deadline(stopping, TimeSpan.FromSeconds(30), $"эмулятор {current} не погас за 30 с");
                await Task.Delay(1000);
            }

            Console.WriteLine($"погашен эмулятор {current}");
            running = false;
        }

        if (!running)
        {
            string avd = wanted ?? avds.FirstOrDefault() ?? throw new UiException("ни одного AVD: emulator -list-avds пуст");

            // Эмулятор живёт дольше помощника и сессии: процесс запускается отдельно
            // и не ждётся. Запуск фоновой командой оболочки умирает вместе с ней
            Process.Start(new ProcessStartInfo(emulator, ["-avd", avd, "-no-boot-anim"]) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
            Console.WriteLine($"запущен эмулятор {avd}");
        }

        Stopwatch clock = Stopwatch.StartNew();
        await AdbAsync("wait-for-device");

        while ((await AdbAsync("shell", "getprop", "sys.boot_completed")).Trim() is not "1")
        {
            Deadline(clock, TimeSpan.FromMinutes(3), "эмулятор не загрузился за 3 минуты");
            await Task.Delay(1000);
        }

        // Анимации дают «could not get idle state» при выгрузке экрана и замедляют каждый переход
        foreach (string scale in (string[])["window_animation_scale", "transition_animation_scale", "animator_duration_scale"])
        {
            await AdbAsync("shell", "settings", "put", "global", scale, "0");
        }

        // После перезапуска эмулятор уезжает в GMT, и операция «сегодня» по часам
        // машины оказывалась для приложения завтрашней — домен её отвергал
        string zone = TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out string? iana) ? iana : TimeZoneInfo.Local.Id;

        if ((await AdbAsync("shell", "getprop", "persist.sys.timezone")).Trim() != zone)
        {
            await AdbAsync("shell", "service", "call", "alarm", "3", "s16", zone);
        }

        Console.WriteLine($"эмулятор {await RunningAvdAsync()} готов за {clock.Elapsed.TotalSeconds:0} с, анимации выключены, пояс {zone}");
    }

    private async Task<bool> IsDeviceAsync() =>
        (await AdbAsync("devices")).Contains("\tdevice", StringComparison.Ordinal);

    // «adb emu avd name» отвечает именем и строкой OK
    private async Task<string?> RunningAvdAsync() =>
        (await AdbAsync("emu", "avd", "name")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

    private async Task DeployAsync()
    {
        string serial = await SerialAsync();
        await AdbAsync("logcat", "-c");

        Stopwatch clock = Stopwatch.StartNew();
        (int exit, string output) = await RunRawAsync(
            "dotnet",
            ["build", "src/Finance.App/Finance.App.csproj", "-f", "net10.0-android", "-t:Run", $"-p:AdbTarget=-s {serial}", "-v", "q", "-nologo"]);

        if (exit is not 0)
        {
            string[] errors = [.. output.Split('\n').Where(static line => line.Contains("error", StringComparison.OrdinalIgnoreCase)).Distinct().Take(20)];
            throw new UiException($"сборка упала:\n{string.Join('\n', errors.Length > 0 ? errors : [output])}");
        }

        Console.WriteLine($"развёрнуто за {clock.Elapsed.TotalSeconds:0} с");
        await WaitForAppAsync();
    }

    private async Task StartAsync()
    {
        await AdbAsync("shell", "am", "force-stop", Package);
        await AdbAsync("logcat", "-c");
        string output = await AdbAsync("shell", "am", "start", "-W", "-n", Activity);
        Console.WriteLine($"холодный старт: {TotalTime(output)} мс");
    }

    private async Task ShortcutAsync(string kind)
    {
        await AdbAsync("shell", "am", "force-stop", Package);
        await AdbAsync("logcat", "-c");
        string output = await AdbAsync("shell", "am", "start", "-W", "-n", Activity, "-a", ShortcutAction, "--es", "kind", kind);
        Console.WriteLine($"ярлык {kind}: {TotalTime(output)} мс");
    }

    /// <summary>
    /// Нарочный сбой отладочной сборки (<c>DebugCrash</c>): намерение с видом и новым номером — с прежним
    /// приложение сбой не повторит. Сбой приходит через две секунды, поэтому команда ждёт пять.
    /// </summary>
    private async Task DebugCrashAsync(string kind)
    {
        await AdbAsync("shell", "am", "start", "-n", Activity, "--es", "debug_crash", kind, "--es", "debug_crash_id", Guid.NewGuid().ToString("N"));
        await Task.Delay(TimeSpan.FromSeconds(5));
        Console.WriteLine($"отладочный сбой {kind}");
    }

    private static string TotalTime(string output) =>
        TotalTimePattern().Match(output) is { Success: true } match ? match.Groups[1].Value : "?";

    private async Task WaitForAppAsync()
    {
        // -t:Run возвращается раньше первого кадра: ждём, пока окно приложения получит фокус
        Stopwatch clock = Stopwatch.StartNew();

        while (!(await AdbAsync("shell", "dumpsys", "window", "displays")).Split('\n')
            .Any(static line => line.Contains("mCurrentFocus", StringComparison.Ordinal) && line.Contains(Package + "/", StringComparison.Ordinal)))
        {
            Deadline(clock, DefaultWait, "окно приложения не появилось — смотри crash");
            await Task.Delay(500);
        }

        Console.WriteLine($"приложение на экране через {clock.Elapsed.TotalSeconds:0.0} с");
    }

    private async Task<List<UiNode>> DumpAsync() => Parse(await DumpXmlAsync());

    private async Task<string> DumpXmlAsync()
    {
        for (int attempt = 1; ; attempt++)
        {
            string raw = await AdbAsync("exec-out", "uiautomator", "dump", "/dev/tty");
            int end = raw.LastIndexOf("</hierarchy>", StringComparison.Ordinal);

            if (end >= 0)
            {
                // После XML uiautomator дописывает строку «UI hierchary dumped to: /dev/tty»
                return raw[..(end + "</hierarchy>".Length)];
            }

            if (attempt is 3)
            {
                throw new UiException($"выгрузка экрана не удалась: {raw.Trim()}");
            }

            await Task.Delay(300);
        }
    }

    private static List<UiNode> Parse(string xml)
    {
        List<UiNode> nodes = [];
        HashSet<XElement> absorbed = [];

        foreach (XElement node in XDocument.Parse(xml).Descendants("node"))
        {
            if (absorbed.Contains(node))
            {
                continue;
            }

            string hint = Attribute(node, "hint");
            string label = OwnLabel(node);
            string id = Attribute(node, "resource-id") is { } rid && rid.IndexOf(":id/", StringComparison.Ordinal) is var at and >= 0 ? rid[(at + 4)..] : string.Empty;
            string kind = Attribute(node, "class");
            bool clickable = Attribute(node, "clickable") is "true";
            bool editable = kind.EndsWith("EditText", StringComparison.Ordinal) || kind.EndsWith("AutoCompleteTextView", StringComparison.Ordinal);
            bool scrollable = Attribute(node, "scrollable") is "true";
            List<string> parts = label.Length > 0 ? [label] : [];

            if (clickable && !editable)
            {
                // Надписи внутри нажимаемой строки становятся её подписью; повтор своей подписи
                // (вкладка и её текст под значком) просто поглощается
                foreach (XElement inner in node.Descendants("node"))
                {
                    string innerLabel = OwnLabel(inner);

                    if (Attribute(inner, "clickable") is "true" || innerLabel.Length is 0 || (label.Length > 0 && innerLabel != label))
                    {
                        continue;
                    }

                    absorbed.Add(inner);

                    if (label.Length is 0)
                    {
                        parts.Add(innerLabel);
                    }
                }
            }

            if (parts.Count is 0 && hint.Length is 0 && !clickable && !editable && !scrollable)
            {
                continue;
            }

            Match bounds = BoundsPattern().Match(Attribute(node, "bounds"));
            int x = (Number(bounds, 1) + Number(bounds, 3)) / 2;
            int y = (Number(bounds, 2) + Number(bounds, 4)) / 2;

            StringBuilder flags = new();
            Flag(flags, clickable, "*");
            Flag(flags, editable, "edit");
            Flag(flags, kind.EndsWith("Switch", StringComparison.Ordinal), "switch");
            Flag(flags, Attribute(node, "checkable") is "true", Attribute(node, "checked") is "true" ? "on" : "off");
            Flag(flags, Attribute(node, "selected") is "true", "selected");
            Flag(flags, Attribute(node, "focused") is "true", "focused");
            Flag(flags, Attribute(node, "enabled") is "false", "disabled");
            Flag(flags, scrollable, "scroll");

            nodes.Add(new UiNode(id, parts, hint, x, y, flags.ToString()));
        }

        return nodes;
    }

    /// <summary>
    /// Собственная подпись узла. Пустое поле отдаёт подсказку и в text: без сверки
    /// заготовка читалась бы набранным значением.
    /// </summary>
    private static string OwnLabel(XElement node) =>
        Attribute(node, "text") is { Length: > 0 } text && text != Attribute(node, "hint") ? text : Attribute(node, "content-desc");

    private static void Flag(StringBuilder flags, bool on, string name)
    {
        if (on)
        {
            flags.Append(flags.Length > 0 ? " " : string.Empty).Append(name);
        }
    }

    private static string Attribute(XElement node, string name) => node.Attribute(name)?.Value ?? string.Empty;

    private static int Number(Match match, int group) => int.Parse(match.Groups[group].ValueSpan, CultureInfo.InvariantCulture);

    private static void Print(List<UiNode> nodes) => Console.Write(Format(nodes));

    private static string Format(List<UiNode> nodes)
    {
        StringBuilder text = new();

        foreach (UiNode node in nodes)
        {
            text.Append(CultureInfo.InvariantCulture, $"{node.X},{node.Y}");
            text.Append(node.Id.Length > 0 ? $" #{node.Id}" : string.Empty);
            text.Append(node.Text.Length > 0 ? $" «{node.Text}»" : string.Empty);
            text.Append(node.Text.Length is 0 && node.Hint.Length > 0 ? $" hint «{node.Hint}»" : string.Empty);
            text.Append(node.Flags.Length > 0 ? $" [{node.Flags}]" : string.Empty);
            text.Append('\n');
        }

        return text.ToString();
    }

    private async Task TapAsync(string target, int times)
    {
        (int x, int y, string what) = await LocateAsync(target);
        string point = $"{x} {y}";

        // Два нажатия одной командой оболочки: между ними ~100 мс, как у торопливого пальца
        string script = string.Join("; ", Enumerable.Repeat($"input tap {point}", times));
        await AdbAsync("shell", script);
        Console.WriteLine($"{(times is 1 ? "нажато" : "нажато дважды")}: {what} ({x},{y})");
    }

    private async Task HoldAsync(string target)
    {
        (int x, int y, string what) = await LocateAsync(target);

        // Дольше секунды лаунчер считает перетаскиванием значка: 600 мс открывают меню ярлыков
        await AdbAsync("shell", "input", "swipe", $"{x}", $"{y}", $"{x}", $"{y}", "600");
        Console.WriteLine($"удержано: {what} ({x},{y})");
    }

    private async Task<(int X, int Y, string What)> LocateAsync(string target)
    {
        if (PointPattern().Match(target) is { Success: true } point)
        {
            return (Number(point, 1), Number(point, 2), target);
        }

        List<UiNode> nodes = await DumpAsync();
        UiNode node = Find(nodes, target) ?? throw new UiException($"на экране нет «{target}». Экран:\n{Render(nodes)}");
        return (node.X, node.Y, node.Id.Length > 0 ? $"#{node.Id} «{node.Label}»" : $"«{node.Label}»");
    }

    /// <summary>
    /// Поиск цели по убыванию строгости: AutomationId, точная подпись, часть подписи.
    /// Неоднозначная часть подписи — ошибка, а не первое попавшееся: нажатие в соседнюю строку
    /// хуже честного отказа.
    /// </summary>
    private static UiNode? Find(List<UiNode> nodes, string target)
    {
        if (nodes.Find(node => node.Id == target) is { } byId)
        {
            return byId;
        }

        target = Plain(target);

        List<UiNode> exact = nodes.FindAll(node => Plain(node.Label) == target || node.Parts.Any(part => Plain(part) == target));

        if (exact.Count > 0)
        {
            // Одинаковая подпись у вкладки и заголовка: нажимаемый узел важнее
            return exact.Find(static node => node.Flags.StartsWith('*')) ?? exact[0];
        }

        List<UiNode> partial = Partial(nodes, target);

        return partial.Count switch
        {
            0 => null,
            1 => partial[0],
            _ => throw new UiException($"«{target}» неоднозначно:\n{Render(partial)}"),
        };
    }

    /// <summary>
    /// Есть ли цель на экране. Для ожидания неоднозначность — не ошибка: сумма стоит
    /// и в итоге, и в строке счёта, и обе говорят, что она появилась.
    /// </summary>
    private static bool IsPresent(List<UiNode> nodes, string target) =>
        nodes.Exists(node => node.Id == target) || Partial(nodes, Plain(target)).Count > 0;

    private static List<UiNode> Partial(List<UiNode> nodes, string target) =>
        nodes.FindAll(node => Plain(node.Label).Contains(target, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Подпись с обычными пробелами. Суммы приложение пишет с неразрывными — между
    /// разрядами и перед знаком валюты, — а в команде набирают обычный пробел.
    /// </summary>
    private static string Plain(string text) => text.Replace(' ', ' ').Replace(' ', ' ');

    private static string Render(List<UiNode> nodes) => Format(nodes).TrimEnd();

    private async Task WaitAsync(string target, bool present)
    {
        Stopwatch clock = Stopwatch.StartNew();

        while (true)
        {
            List<UiNode> nodes = await DumpAsync();
            bool found = IsPresent(nodes, target);

            if (found == present)
            {
                Console.WriteLine($"{(present ? "появилось" : "исчезло")} «{target}» через {clock.Elapsed.TotalSeconds:0.0} с");
                return;
            }

            Deadline(clock, DefaultWait, $"«{target}» {(present ? "не появилось" : "не исчезло")} за {DefaultWait.TotalSeconds:0} с. Экран:\n{Render(nodes)}");
        }
    }

    private async Task TypeAsync(string text)
    {
        if (text.Any(static c => c > 127))
        {
            throw new UiException("input text не передаёт не-ASCII: названия для проверки набираются латиницей");
        }

        await AdbAsync("shell", "input", "text", text.Replace(" ", "%s", StringComparison.Ordinal));
        Console.WriteLine($"набрано: {text}");
    }

    private async Task KeyAsync(string key)
    {
        string code = key switch
        {
            "back" => "4",
            "enter" => "66",
            "del" => "67",
            "hide" => "111",
            "home" => "3",
            _ => throw new UiException($"неизвестная клавиша {key}: back | enter | del | hide | home"),
        };

        await AdbAsync("shell", "input", "keyevent", code);

        // Уход клавиатуры перекладывает страницу: прокрутка, посланная сразу, теряется молча
        await Task.Delay(500);
        Console.WriteLine($"клавиша: {key}");
    }

    private async Task SwipeAsync(string direction)
    {
        (int from, int to) = direction switch
        {
            "up" => (1700, 700),
            "down" => (700, 1700),
            _ => throw new UiException($"прокрутка {direction}: up | down"),
        };

        await AdbAsync("shell", "input", "swipe", "540", $"{from}", "540", $"{to}", "300");
        Console.WriteLine($"прокрутка: {direction}");
    }

    private async Task QueryAsync(string sql)
    {
        // Свежие записи лежат в журнале WAL, а не в самом файле базы: без -wal копия пуста
        string folder = Path.Combine(Path.GetTempPath(), "finance-ui-db");
        Directory.CreateDirectory(folder);

        foreach (string file in (string[])["finance.db", "finance.db-wal", "finance.db-shm"])
        {
            byte[] bytes = await AdbBytesAsync("exec-out", "run-as", Package, "cat", $"files/{file}");
            await File.WriteAllBytesAsync(Path.Combine(folder, file), bytes);
        }

        await using SqliteConnection connection = new($"Data Source={Path.Combine(folder, "finance.db")};Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        try
        {
            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            StringBuilder text = new();
            text.AppendJoin('\t', Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)).Append('\n');
            int rows = 0;

            while (await reader.ReadAsync())
            {
                text.AppendJoin('\t', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture))).Append('\n');
                rows++;
            }

            Console.Write(text);
            Console.WriteLine($"строк: {rows}");
        }
        catch (SqliteException error)
        {
            throw new UiException($"запрос не выполнен: {error.Message}");
        }
    }

    private async Task CrashAsync()
    {
        string report = await CrashReportAsync();

        Console.WriteLine(report.Length > 0 ? Trim(report, 60) : "падений в журнале нет (журнал чистится командами run, start и shortcut)");
    }

    /// <summary>
    /// Проверка сценария: падение молчит — окно просто закрывается, — и шаг за ним
    /// упал бы по таймауту с непонятной причиной. Поэтому сценарий кончается этой командой.
    /// </summary>
    private async Task NoCrashAsync()
    {
        string report = await CrashReportAsync();

        if (report.Length > 0)
        {
            throw new UiException($"в журнале падения:\n{Trim(report, 30)}");
        }

        Console.WriteLine("падений нет");
    }

    private async Task<string> CrashReportAsync()
    {
        // Буфер crash держит падения процесса, в том числе необработанное исключение .NET,
        // которое доходит до Java обёрткой; сам рантайм пишет своё тегами DOTNET и mono-rt,
        // а сбои, перехваченные приложением и показанные сообщением, — тегом Finance.
        // Тег AndroidRuntime из основного буфера не нужен — он повторил бы буфер crash
        string java = await AdbAsync("logcat", "-d", "-b", "crash");
        string managed = await AdbAsync("logcat", "-d", "DOTNET:E", "mono-rt:E", "Finance:E", "*:S");
        return string.Join('\n', $"{java}\n{managed}".Split('\n')
            .Select(static line => line.TrimEnd())
            .Where(static line => line.Length > 0 && !line.StartsWith("--------- beginning", StringComparison.Ordinal)));
    }

    private static string Trim(string text, int lines)
    {
        string[] all = text.Trim().Split('\n');
        return all.Length <= lines ? string.Join('\n', all) : string.Join('\n', all[..lines]) + $"\n… ещё {all.Length - lines} строк";
    }

    private async Task ShotAsync(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        await File.WriteAllBytesAsync(file, await AdbBytesAsync("exec-out", "screencap", "-p"));
        Console.WriteLine($"снимок: {file}");
    }

    /// <summary>
    /// Нижние края прокручиваемых областей и кнопок Add*/Save*. Устаревший отступ снизу
    /// (страница вернулась из-под экрана без панели вкладок) подписей не меняет — dump
    /// его не видит, — а края поднимает на высоту системной полосы.
    /// </summary>
    private async Task<string> EdgesAsync()
    {
        List<string> edges = [];

        foreach (XElement node in XDocument.Parse(await DumpXmlAsync()).Descendants("node"))
        {
            string kind = Attribute(node, "class");
            string id = Attribute(node, "resource-id") is { } rid && rid.IndexOf(":id/", StringComparison.Ordinal) is var at and >= 0 ? rid[(at + 4)..] : string.Empty;
            bool scroll = Attribute(node, "scrollable") is "true" || kind.EndsWith("RecyclerView", StringComparison.Ordinal) || kind.EndsWith("ScrollView", StringComparison.Ordinal);

            if (scroll || id.StartsWith("Add", StringComparison.Ordinal) || id.StartsWith("Save", StringComparison.Ordinal))
            {
                string name = id.Length > 0 ? $"#{id}" : kind[(kind.LastIndexOf('.') + 1)..];
                edges.Add($"{name} {Number(BoundsPattern().Match(Attribute(node, "bounds")), 4)}");
            }
        }

        return string.Join(" | ", edges);
    }

    private async Task SameEdgesAsync()
    {
        string was = _edges ?? throw new UiException("same сверяет с прошлым edges, а его в этой цепочке не было");
        string now = await EdgesAsync();

        if (now != was)
        {
            throw new UiException($"края сдвинулись — устаревший отступ снизу?\n  было: {was}\n  стало: {now}");
        }

        Console.WriteLine($"края на месте: {now}");
    }

    /// <summary>
    /// Переход под замедленной вдесятеро анимацией и снимки подряд. На время анимированного
    /// перехода Shell заливает область страниц чёрным, и сквозь прозрачную шапку это видно
    /// полосой под строкой состояния. С выключенными анимациями, как их ставит boot,
    /// заливки нет вовсе — поэтому переход и замедляется, а не снимается как есть.
    /// </summary>
    private async Task TransitAsync(string target)
    {
        (int x, int y, string what) = target is "back" ? (0, 0, "назад") : await LocateAsync(target);

        int black = 0;
        string darkest = string.Empty;
        int darkestSum = int.MaxValue;

        await AnimationsAsync("10");

        try
        {
            await AdbAsync("shell", target is "back" ? "input keyevent 4" : $"input tap {x} {y}");

            for (int frame = 0; frame < 8; frame++)
            {
                (int r, int g, int b) = StripColor(await AdbBytesAsync("exec-out", "screencap"));

                if (r + g + b < 30)
                {
                    black++;
                }

                if (r + g + b < darkestSum)
                {
                    darkestSum = r + g + b;
                    darkest = $"{r},{g},{b}";
                }
            }
        }
        finally
        {
            // Сбой снимка не должен оставить эмулятор с анимацией вдесятеро медленнее
            await AnimationsAsync("0");
        }

        // Замедленный переход ещё доигрывает: следующая команда цепочки ждала бы его
        await Task.Delay(1500);

        if (black > 0)
        {
            throw new UiException($"переход «{what}»: полоса под строкой состояния чёрная на {black} кадрах из 8");
        }

        Console.WriteLine($"переход «{what}»: чёрных кадров нет, самый тёмный цвет полосы {darkest}");
    }

    private async Task AnimationsAsync(string scale) =>
        await AdbAsync("shell", $"settings put global window_animation_scale {scale}; settings put global transition_animation_scale {scale}; settings put global animator_duration_scale {scale}");

    /// <summary>
    /// Средний цвет полосы под строкой состояния из сырого снимка screencap: заголовок —
    /// ширина, высота, формат и (с Android 9) цветовое пространство, дальше RGBA построчно.
    /// Размер заголовка выводится из длины: так он не зависит от версии системы.
    /// </summary>
    private static (int R, int G, int B) StripColor(byte[] raw)
    {
        int width = raw.Length >= 8 ? BitConverter.ToInt32(raw, 0) : 0;
        int height = raw.Length >= 8 ? BitConverter.ToInt32(raw, 4) : 0;
        int header = raw.Length - (width * height * 4);

        if (width <= 0 || height < 200 || header is < 8 or > 64)
        {
            throw new UiException($"снимок screencap не разобран: {raw.Length} байт, {width}×{height}");
        }

        long r = 0, g = 0, b = 0, n = 0;

        // Полоса высотой в строку состояния посередине экрана: по краям — значки и часы
        for (int y = 5; y < height / 20; y += 5)
        {
            for (int x = width * 3 / 8; x < width * 5 / 8; x += 10)
            {
                int at = header + (((y * width) + x) * 4);
                r += raw[at];
                g += raw[at + 1];
                b += raw[at + 2];
                n++;
            }
        }

        return ((int)(r / n), (int)(g / n), (int)(b / n));
    }

    private async Task<string> SerialAsync() =>
        (await AdbAsync("devices")).Split('\n')
            .Select(static line => line.Split('\t'))
            .FirstOrDefault(static parts => parts.Length is 2 && parts[1].Trim() is "device")?[0]
        ?? throw new UiException("устройство не запущено: dotnet tools/ui.cs -- boot");

    private static void Deadline(Stopwatch clock, TimeSpan limit, string message)
    {
        if (clock.Elapsed > limit)
        {
            throw new UiException(message);
        }
    }

    private async Task<string> AdbAsync(params string[] arguments) => await RunAsync(_adb, arguments);

    private static async Task<string> RunAsync(string file, string[] arguments)
    {
        (int exit, string output) = await RunRawAsync(file, arguments);
        return exit is 0 ? output : throw new UiException($"{Path.GetFileName(file)} {string.Join(' ', arguments)}: {output.Trim()}");
    }

    private static async Task<(int Exit, string Output)> RunRawAsync(string file, string[] arguments)
    {
        ProcessStartInfo start = new(file, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using Process process = Process.Start(start) ?? throw new UiException($"не запустился {file}");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, (await output) + (await error));
    }

    private async Task<byte[]> AdbBytesAsync(params string[] arguments)
    {
        ProcessStartInfo start = new(_adb, arguments) { RedirectStandardOutput = true };
        using Process process = Process.Start(start) ?? throw new UiException("не запустился adb");
        using MemoryStream buffer = new();
        await process.StandardOutput.BaseStream.CopyToAsync(buffer);
        await process.WaitForExitAsync();
        return process.ExitCode is 0 ? buffer.ToArray() : throw new UiException($"adb {string.Join(' ', arguments)} завершился с кодом {process.ExitCode}");
    }

    /// <summary>
    /// Путь к утилите SDK: ANDROID_HOME, если задан, иначе установка Visual Studio на этой машине.
    /// </summary>
    private static string Sdk(string folder, string file)
    {
        string root = Environment.GetEnvironmentVariable("ANDROID_HOME") is { Length: > 0 } home
            ? home
            : @"C:\Program Files (x86)\Android\android-sdk";
        string path = Path.Combine(root, folder, file);
        return File.Exists(path) ? path : Path.GetFileNameWithoutExtension(file);
    }

    [GeneratedRegex(@"\[(\d+),(\d+)\]\[(\d+),(\d+)\]")]
    private static partial Regex BoundsPattern();

    [GeneratedRegex(@"^(\d+),(\d+)$")]
    private static partial Regex PointPattern();

    [GeneratedRegex(@"TotalTime: (\d+)")]
    private static partial Regex TotalTimePattern();
}

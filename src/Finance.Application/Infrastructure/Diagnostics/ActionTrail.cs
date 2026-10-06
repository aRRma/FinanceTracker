using System.Globalization;
using System.Text;
using Finance.Domain.Errors;

namespace Finance.Application.Infrastructure.Diagnostics;

/// <summary>
/// След действий: последние события перед сбоем — переходы, нажатия, уход в фон и возврат. Живёт в памяти
/// и выписывается в отчёт о сбое; на диск постоянно не пишется — каждое событие было бы лишней записью во флеш.
/// </summary>
/// <remarks>
/// Принимает только имена из кода: маршрут без параметров, файл и метод, член перечисления. Свободной строки
/// нет, поэтому сумму или имя счёта сюда не положить даже случайно. Кроме того, след держит текущий экран и
/// последнее действие — короткую сводку, которую система отдаёт назад даже после нативного падения.
/// </remarks>
public sealed class ActionTrail
{
    /// <summary>
    /// Сколько последних событий хранится.
    /// </summary>
    public const int Capacity = 30;

    /// <summary>
    /// Предел сводки в байтах: больше система не принимает — проверено на эмуляторе.
    /// </summary>
    public const int SummaryLimit = 128;

    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly string[] _ring = new string[Capacity];
    private int _next;
    private int _count;
    private string _screen = "";
    private string _lastAction = "";

    /// <summary>
    /// Пустой след.
    /// </summary>
    /// <param name="time">Источник момента.</param>
    public ActionTrail(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        _time = time;
    }

    /// <summary>
    /// Сменились экран или последнее действие — платформе пора обновить сводку для системы.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Переход на экран.
    /// </summary>
    /// <param name="location">Адрес экрана; параметры после <c>?</c> отрезаются — в них ключи записей.</param>
    public void Navigated(string location)
    {
        ArgumentNullException.ThrowIfNull(location);

        int query = location.IndexOf('?', StringComparison.Ordinal);
        string route = query < 0 ? location : location[..query];

        Add("nav", route, screen: route);
    }

    /// <summary>
    /// Обработчик события: нажатие, появление экрана, прокрутка до конца списка.
    /// </summary>
    /// <param name="file">Путь к файлу обработчика; остаётся только имя.</param>
    /// <param name="member">Имя обработчика.</param>
    public void Action(string file, string member)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(member);

        // Путь собран на машине сборки: разделитель в нём может быть и не местный
        string name = file[(file.LastIndexOfAny(['/', '\\']) + 1)..];
        name = name.EndsWith(".xaml.cs", StringComparison.Ordinal) ? name[..^".xaml.cs".Length]
            : name.EndsWith(".cs", StringComparison.Ordinal) ? name[..^".cs".Length]
            : name;

        Add("run", string.Create(CultureInfo.InvariantCulture, $"{name}.{member}"), action: true);
    }

    /// <summary>
    /// Событие жизни приложения.
    /// </summary>
    /// <param name="lifecycle">Событие.</param>
    public void Lifecycle(LifecycleEvent lifecycle) => Add("life", lifecycle.ToString());

    /// <summary>
    /// Нарушенное правило — только его имя: в тексте правила бывают имена счетов и групп.
    /// </summary>
    /// <param name="rule">Правило.</param>
    public void RuleBroken(Invariant rule) => Add("rule", rule.ToString());

    /// <summary>
    /// События от старых к новым.
    /// </summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            string[] events = new string[_count];
            int first = (_next - _count + Capacity) % Capacity;

            for (int index = 0; index < _count; index++)
            {
                events[index] = _ring[(first + index) % Capacity];
            }

            return events;
        }
    }

    /// <summary>
    /// Сводка для системы: текущий экран и последнее действие, не длиннее <see cref="SummaryLimit"/> байт UTF-8.
    /// </summary>
    /// <remarks>
    /// Режется по символам, а не по байтам: половина многобайтного символа сделала бы строку нечитаемой.
    /// </remarks>
    public byte[] Summary()
    {
        string summary;

        lock (_gate)
        {
            summary = string.Create(CultureInfo.InvariantCulture, $"{_screen} | {_lastAction}");
        }

        int length = 0;
        StringBuilder kept = new();

        foreach (Rune rune in summary.EnumerateRunes())
        {
            length += rune.Utf8SequenceLength;

            if (length > SummaryLimit)
            {
                break;
            }

            kept.Append(rune.ToString());
        }

        return Encoding.UTF8.GetBytes(kept.ToString());
    }

    private void Add(string kind, string value, string? screen = null, bool action = false)
    {
        string entry = string.Create(CultureInfo.InvariantCulture, $"{_time.GetUtcNow():HH:mm:ss.fff} {kind} {value}");

        lock (_gate)
        {
            _ring[_next] = entry;
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);

            if (screen is not null)
            {
                _screen = screen;
            }

            if (action)
            {
                _lastAction = value;
            }
        }

        if (screen is not null || action)
        {
            Changed?.Invoke();
        }
    }
}

namespace Finance.Application.Tests;

/// <summary>
/// Часы тестов: момент задан, а не взят у машины. На настоящих часах «сегодня»
/// зависело от дня запуска, и прогон около полуночи последнего числа заводил
/// операцию в одном месяце, а отчёт читал за другой.
/// </summary>
/// <remarks>
/// Каждое чтение сдвигает время на миллисекунду. Две записи подряд не получают одну
/// метку, как и на настоящих часах: иначе проверка «метка не переписана» проходила бы
/// и тогда, когда метку переписали тем же значением, а порядок ленты по
/// <c>created_at_utc</c> решался бы случайной частью ключа. До смены суток так
/// не дойти: полдень UTC — это та же дата в любой зоне от −12 до +11.
/// </remarks>
internal sealed class TestTime : TimeProvider
{
    /// <summary>
    /// Отправная точка: первое чтение вернёт её же плюс миллисекунду. Середина месяца:
    /// прошлый месяц целиком позади, до границы следующего далеко в обе стороны.
    /// </summary>
    public static readonly DateTimeOffset Start = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private long _ticks = Start.UtcTicks;

    public override DateTimeOffset GetUtcNow() =>
        new(Interlocked.Add(ref _ticks, TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
}

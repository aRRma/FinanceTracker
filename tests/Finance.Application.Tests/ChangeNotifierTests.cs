using Finance.Application.Infrastructure;

namespace Finance.Application.Tests;

/// <summary>Оповещение об изменении данных доходит до подписчиков тем путём, который задала платформа.</summary>
public sealed class ChangeNotifierTests
{
    /// <summary>
    /// Событие идёт через переданную доставку: команды выполняются в фоне,
    /// а подписчики правят привязанные коллекции, и без доставки в поток
    /// интерфейса это роняло бы разметку.
    /// </summary>
    [Fact]
    public void Событие_доставляется_через_переданный_способ()
    {
        List<string> trace = [];
        ChangeNotifier notifier = new(action =>
        {
            trace.Add("доставка");
            action();
        });

        notifier.Changed += change => trace.Add($"подписчик:{change}");
        notifier.Publish(DataChange.Accounts);

        Assert.Equal(["доставка", "подписчик:Accounts"], trace);
    }

    /// <summary>Пустое изменение не будит подписчиков: перечитывать им нечего.</summary>
    [Fact]
    public void Пустое_изменение_не_публикуется()
    {
        int delivered = 0;
        ChangeNotifier notifier = new(action =>
        {
            delivered++;
            action();
        });

        notifier.Publish(DataChange.None);

        Assert.Equal(0, delivered);
    }
}

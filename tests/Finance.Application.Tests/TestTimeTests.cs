using Finance.Application.Infrastructure;

namespace Finance.Application.Tests;

/// <summary>
/// Тестовые часы действительно доходят до приложения. Регистрация через TryAdd
/// молча уступает первому источнику времени, и без этой проверки замена на Add
/// вернула бы тесты на настоящие часы, не уронив ни одного.
/// </summary>
public sealed class TestTimeTests
{
    [Fact]
    public async Task Приложение_читает_время_с_тестовых_часов()
    {
        await using TestDatabase database = TestDatabase.CreateUnprepared();
        IClock clock = database.Resolve<IClock>();

        DateOnly expected = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(TestTime.Start, clock.TimeZone).DateTime);

        Assert.Equal(expected, clock.Today);
        Assert.InRange(clock.NowUtc, TestTime.Start, TestTime.Start.AddMinutes(1));
    }

    [Fact]
    public void Каждое_чтение_позже_предыдущего()
    {
        TestTime time = new();

        DateTimeOffset first = time.GetUtcNow();

        Assert.True(time.GetUtcNow() > first, "два чтения подряд вернули одну метку");
    }
}

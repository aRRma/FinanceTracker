namespace Finance.Domain.Tests;

/// <summary>Ключи сущностей: UUIDv7 с клиента и детерминированный UUIDv5 для стартового набора.</summary>
public sealed class KeysTests
{
    /// <summary>Пространство имён DNS из RFC 9562 — для эталонного вектора.</summary>
    private static readonly Guid DnsNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    [Fact]
    public void Новый_ключ_имеет_версию_семь()
    {
        Guid key = Keys.New();

        // Полубайт версии — старший полубайт седьмого байта в сетевом порядке
        byte versionNibble = (byte)(key.ToByteArray(bigEndian: true)[6] >> 4);

        Assert.Equal(7, versionNibble);
    }

    [Fact]
    public void Новые_ключи_не_повторяются()
    {
        Guid[] keys = Enumerable.Range(0, 1000).Select(_ => Keys.New()).ToArray();

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void Новый_ключ_несёт_текущее_время_в_старших_байтах()
    {
        // Монотонность — не украшение: на ней держится локальность вставок в индекс
        // и разрыв ничьей в сортировке ленты. Проверяется метка внутри
        // ключа, а не порядок двух вызовов подряд: часы Windows идут шагом около
        // 15 мс, оба ключа попали бы в один тик, и тест мигал бы. Случайный Guid
        // такую проверку не проходит — старшие байты у него не время
        byte[] bytes = Keys.New().ToByteArray(bigEndian: true);

        long milliseconds = 0;
        foreach (byte part in bytes.AsSpan(0, 6))
        {
            milliseconds = (milliseconds << 8) | part;
        }

        DateTimeOffset stamp = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);

        Assert.True(
            (DateTimeOffset.UtcNow - stamp).Duration() < TimeSpan.FromMinutes(1),
            $"метка ключа {stamp:O} далека от текущего времени");
    }

    /// <summary>
    /// Эталонный вектор UUIDv5 из RFC 9562. Проверяет реализацию независимо от
    /// нашего стартового набора: если сойдётся только с набором, ошибка в алгоритме
    /// и ошибка в наборе взаимно замаскируются.
    /// </summary>
    [Fact]
    public void Выведенный_ключ_совпадает_с_эталоном_RFC()
    {
        Guid derived = Keys.Derive(DnsNamespace, "www.example.org");

        Assert.Equal(Guid.Parse("74738ff5-5367-5958-9aee-98fffdcd1876"), derived);
    }

    [Fact]
    public void Выведенный_ключ_имеет_версию_пять()
    {
        Guid derived = Keys.Derive(DnsNamespace, "www.example.org");

        byte[] bytes = derived.ToByteArray(bigEndian: true);

        Assert.Equal(5, bytes[6] >> 4);
        Assert.Equal(0b10, bytes[8] >> 6);
    }

    [Fact]
    public void Выведенный_ключ_детерминирован()
    {
        Assert.Equal(
            Keys.Derive(DnsNamespace, "food.groceries"),
            Keys.Derive(DnsNamespace, "food.groceries"));
    }

    [Fact]
    public void Разные_имена_дают_разные_ключи()
    {
        Assert.NotEqual(
            Keys.Derive(DnsNamespace, "food.groceries"),
            Keys.Derive(DnsNamespace, "food.coffee"));
    }

    [Fact]
    public void Разные_пространства_имён_дают_разные_ключи()
    {
        Guid other = Guid.Parse("6f1b2c40-9a3d-5e77-8c21-4b0e7d5a9f13");

        Assert.NotEqual(
            Keys.Derive(DnsNamespace, "food.groceries"),
            Keys.Derive(other, "food.groceries"));
    }
}

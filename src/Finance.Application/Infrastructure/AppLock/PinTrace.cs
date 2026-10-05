using System.Globalization;
using System.Security.Cryptography;

namespace Finance.Application.Infrastructure.AppLock;

/// <summary>
/// След ПИН-кода: PBKDF2-SHA256 со своей солью. Сам код не хранится нигде.
/// </summary>
/// <remarks>
/// Число итераций записано в самом следе: его можно поднять в новой версии,
/// и прежний след продолжит проверяться со своим.
/// </remarks>
public sealed class PinTrace
{
    /// <summary>
    /// Итераций у нового следа. Цель — проверка кода на телефоне не дольше трети секунды; на телефоне не мерилось.
    /// </summary>
    public const int Iterations = 100_000;

    private const string Version = "1";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly int _iterations;
    private readonly byte[] _salt;
    private readonly byte[] _hash;

    private PinTrace(int iterations, byte[] salt, byte[] hash)
    {
        _iterations = iterations;
        _salt = salt;
        _hash = hash;
    }

    /// <summary>
    /// Снимает след с нового кода — с новой солью при каждом задании.
    /// </summary>
    /// <param name="pin">ПИН-код.</param>
    public static PinTrace Create(string pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        return new PinTrace(Iterations, salt, Derive(pin, salt, Iterations));
    }

    /// <summary>
    /// Разбирает записанный след. Испорченный или чужой формат — <c>null</c>.
    /// </summary>
    /// <param name="text">Строка из хранилища.</param>
    public static PinTrace? Parse(string? text)
    {
        if (text?.Split('.') is not [Version, string iterations, string salt, string hash])
        {
            return null;
        }

        try
        {
            return int.TryParse(iterations, NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0
                ? new PinTrace(count, Convert.FromBase64String(salt), Convert.FromBase64String(hash))
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Совпадает ли код со следом. Сравнение — за постоянное время.
    /// </summary>
    /// <param name="pin">Набранный код.</param>
    public bool Matches(string pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return CryptographicOperations.FixedTimeEquals(Derive(pin, _salt, _iterations), _hash);
    }

    /// <summary>
    /// Строка для хранилища: версия, итерации, соль и след.
    /// </summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Version}.{_iterations}.{Convert.ToBase64String(_salt)}.{Convert.ToBase64String(_hash)}");

    private static byte[] Derive(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, HashSize);
}

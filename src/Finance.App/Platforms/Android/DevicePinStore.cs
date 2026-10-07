using Finance.Application.Infrastructure.AppLock;

namespace Finance.App;

/// <summary>
/// След ПИН-кода в <see cref="SecureStorage"/>: его шифрует ключ из хранилища ключей Android,
/// и с выгрузкой он не уезжает.
/// </summary>
internal sealed class DevicePinStore : IPinStore
{
    private const string Name = "app_lock.pin";

    /// <inheritdoc />
    /// <remarks>
    /// Нерасшифровываемый след (ключ Android потерян или значение испорчено) MAUI
    /// обычно стирает сам и отдаёт <c>null</c>, но закэшированное испорченное значение
    /// бросает исключение. Оно приравнено к отсутствию следа: иначе каждая попытка
    /// входа падала бы, и заслонку не снял бы и верный код.
    /// </remarks>
    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await SecureStorage.Default.GetAsync(Name);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Тот же тег, что у Guarded, но уровень Warn: crash помощника эмулятора берёт только
            // уровень Error этого тега и такой случай не покажет — это не падение, вход продолжается
            Android.Util.Log.Warn("Finance", error.ToString());
            CrashCatcher.Warn(error);

            SecureStorage.Default.Remove(Name);

            return null;
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(string trace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await SecureStorage.Default.SetAsync(Name, trace);
    }

    /// <inheritdoc />
    public void Remove() => SecureStorage.Default.Remove(Name);
}

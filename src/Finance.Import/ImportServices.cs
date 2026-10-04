using Finance.Import.Wallet;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Import;

/// <summary>
/// Подключение переноса к службам прикладного слоя. Вызывается после
/// <c>AddFinance</c>: перенос пишет в ту же базу теми же хранилищем и часами.
/// </summary>
public static class ImportServices
{
    /// <summary>
    /// Регистрирует перенос из Wallet.
    /// </summary>
    /// <param name="services">Набор служб с уже подключённым прикладным слоем.</param>
    public static IServiceCollection AddWalletImport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWalletImportHandler, WalletImportHandler>();

        return services;
    }
}

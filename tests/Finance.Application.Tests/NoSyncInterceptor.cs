using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Finance.Application.Tests;

/// <summary>
/// Открывает соединения тестовых баз без сброса на диск (<c>synchronous=OFF</c>).
/// Параллельные сбросы на рабочей машине шли в десять с лишним раз медленнее
/// одиночных, и набор упирался в диск (замер сентября 2026). Надёжность при обрыве
/// питания тестам не нужна, а смысл запросов, индексов и копий от неё не зависит.
/// </summary>
internal sealed class NoSyncInterceptor : DbConnectionInterceptor
{
    private const string Pragma = "PRAGMA synchronous=OFF;";

    /// <summary>
    /// Один экземпляр на все тесты: перехватчик входит в параметры EF, и новый
    /// экземпляр на каждую базу заставил бы EF строить внутренние службы заново.
    /// </summary>
    public static NoSyncInterceptor Instance { get; } = new();

    private NoSyncInterceptor()
    {
    }

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = Pragma;
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = Pragma;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

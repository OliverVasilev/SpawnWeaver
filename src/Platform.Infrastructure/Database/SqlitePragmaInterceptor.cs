using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Platform.Infrastructure.Database;

/// <summary>
/// Applies concurrency-friendly PRAGMAs to every SQLite connection as it opens:
/// <c>busy_timeout</c> makes a writer wait for a held lock instead of failing immediately with
/// "database is locked", and WAL journaling lets readers run alongside a writer. This keeps the
/// file-backed SQLite database (local dev + the integration tests, which fire many concurrent
/// requests at one database) from throwing under write contention.
/// </summary>
internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA busy_timeout = 10000; PRAGMA journal_mode = WAL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Apply(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
    }
}

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;

namespace Sales.Dal.Repositories;

/// <summary>
/// SQL Server реалізація ідемпотентності споживача через таблицю dbo.ProcessedEvents (EventId UNIQUEIDENTIFIER PRIMARY KEY).
/// Порушення первинного ключа (SqlException 2627 / 2601) означає повторну доставку події.
/// </summary>
public sealed class ProcessedEventRepository(
    SqlConnection connection,
    Func<SqlTransaction?> transactionProvider,
    DalOptions? options = null) : IProcessedEventRepository
{
    private readonly int _commandTimeout = options?.CommandTimeoutSeconds ?? 30;
    private static volatile bool _tableEnsured;

    public async Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var transaction = transactionProvider();

        if (!_tableEnsured)
        {
            const string ensureTableSql = """
                IF OBJECT_ID('dbo.ProcessedEvents', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ProcessedEvents (
                        EventId     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        EventType   NVARCHAR(150) NOT NULL,
                        ProcessedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                ensureTableSql,
                transaction: transaction,
                commandTimeout: _commandTimeout,
                cancellationToken: ct));

            _tableEnsured = true;
        }

        const string insertSql = """
            INSERT INTO dbo.ProcessedEvents (EventId, EventType, ProcessedAt)
            VALUES (@EventId, @EventType, SYSUTCDATETIME());
            """;

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { EventId = eventId, EventType = eventType },
                transaction: transaction,
                commandTimeout: _commandTimeout,
                cancellationToken: ct));

            return true;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // Порушення PRIMARY KEY / UNIQUE: подія з цим EventId вже була успішно оброблена
            return false;
        }
    }
}

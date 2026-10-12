using Dapper;
using Microsoft.Data.SqlClient;
using Sales.Dal.Helpers;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;

namespace Sales.Dal.Repositories;

/// <summary>
/// Базовий Dapper-репозиторій, що реалізує спільні CRUD-операції через параметризовані CommandDefinition.
/// Імена таблиць, стовпців та умови задаються виключно як константи коду при створенні нащадків.
/// </summary>
public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    private readonly string _tableName;
    private readonly string _idColumn;
    private readonly string _selectColumns;
    private readonly string _insertColumns;
    private readonly string _insertValues;
    private readonly string _updateSetClause;
    private readonly bool _hasSoftDelete;

    protected BaseDapperRepository(
        SqlConnection connection,
        Func<SqlTransaction?> transactionProvider,
        string tableName,
        string idColumn,
        string selectColumns,
        string insertColumns,
        string insertValues,
        string updateSetClause,
        bool hasSoftDelete,
        DalOptions? options = null)
        : base(connection, transactionProvider, options)
    {
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _idColumn = idColumn ?? throw new ArgumentNullException(nameof(idColumn));
        _selectColumns = selectColumns ?? throw new ArgumentNullException(nameof(selectColumns));
        _insertColumns = insertColumns ?? throw new ArgumentNullException(nameof(insertColumns));
        _insertValues = insertValues ?? throw new ArgumentNullException(nameof(insertValues));
        _updateSetClause = updateSetClause ?? throw new ArgumentNullException(nameof(updateSetClause));
        _hasSoftDelete = hasSoftDelete;
    }

    protected string TableName => _tableName;
    protected string IdColumn => _idColumn;
    protected string SelectColumns => _selectColumns;
    protected bool HasSoftDelete => _hasSoftDelete;

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var sql = _hasSoftDelete
                ? $"SELECT {_selectColumns} FROM {_tableName} WHERE {_idColumn} = @Id AND IsDeleted = 0;"
                : $"SELECT {_selectColumns} FROM {_tableName} WHERE {_idColumn} = @Id;";

            var cmd = new CommandDefinition(
                sql,
                new { Id = id },
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            return await Connection.QuerySingleOrDefaultAsync<T>(cmd);
        });
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var sql = _hasSoftDelete
                ? $"SELECT {_selectColumns} FROM {_tableName} WHERE IsDeleted = 0;"
                : $"SELECT {_selectColumns} FROM {_tableName};";

            var cmd = new CommandDefinition(
                sql,
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            var result = await Connection.QueryAsync<T>(cmd);
            return result.AsList();
        });
    }

    public virtual async Task<long> AddAsync(T entity, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var sql = $"INSERT INTO {_tableName} ({_insertColumns}) OUTPUT INSERTED.{_idColumn} VALUES ({_insertValues});";

            var cmd = new CommandDefinition(
                sql,
                entity,
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            return await Connection.ExecuteScalarAsync<long>(cmd);
        });
    }

    public virtual async Task<bool> UpdateAsync(T entity, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var sql = _hasSoftDelete
                ? $"UPDATE {_tableName} SET {_updateSetClause} WHERE {_idColumn} = @Id AND IsDeleted = 0;"
                : $"UPDATE {_tableName} SET {_updateSetClause} WHERE {_idColumn} = @Id;";

            var cmd = new CommandDefinition(
                sql,
                entity,
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            var rows = await Connection.ExecuteAsync(cmd);
            return rows > 0;
        });
    }

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var sql = _hasSoftDelete
                ? $"UPDATE {_tableName} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE {_idColumn} = @Id AND IsDeleted = 0;"
                : $"DELETE FROM {_tableName} WHERE {_idColumn} = @Id;";

            var cmd = new CommandDefinition(
                sql,
                new { Id = id },
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            var rows = await Connection.ExecuteAsync(cmd);
            return rows > 0;
        });
    }
}

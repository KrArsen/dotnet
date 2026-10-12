using System.Data;
using Microsoft.Data.SqlClient;
using Sales.Dal.Options;

namespace Sales.Dal.Repositories;

public abstract class RepositoryBase
{
    private readonly SqlConnection _connection;
    private readonly Func<SqlTransaction?> _transactionProvider;
    private readonly DalOptions _options;

    protected RepositoryBase(
        SqlConnection connection,
        Func<SqlTransaction?> transactionProvider,
        DalOptions? options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transactionProvider = transactionProvider ?? throw new ArgumentNullException(nameof(transactionProvider));
        _options = options ?? new DalOptions();
    }

    protected SqlConnection Connection => _connection;

    protected SqlTransaction? Transaction => _transactionProvider();

    protected int CommandTimeout => _options.CommandTimeoutSeconds;

    protected async Task EnsureOpenAsync(CancellationToken ct = default)
    {
        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
        }
    }
}

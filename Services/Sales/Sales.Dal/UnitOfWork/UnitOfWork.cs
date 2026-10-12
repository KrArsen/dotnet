using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Dal.Repositories;

namespace Sales.Dal.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private readonly bool _ownsConnection;
    private readonly DalOptions _options;
    private SqlTransaction? _transaction;

    private IClientRepository? _clients;
    private IDealRepository? _deals;
    private IProductRepository? _products;
    private ITeamMemberRepository? _teamMembers;

    public UnitOfWork(string connectionString, IOptions<DalOptions>? options = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        _connection = new SqlConnection(connectionString);
        _ownsConnection = true;
        _options = options?.Value ?? new DalOptions();
    }

    public UnitOfWork(SqlConnection connection, DalOptions? options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _ownsConnection = false;
        _options = options ?? new DalOptions();
    }

    public IClientRepository Clients => _clients ??= new ClientRepository(_connection, () => _transaction, _options);

    public IDealRepository Deals => _deals ??= new DealRepository(_connection, () => _transaction, _options);

    public IProductRepository Products => _products ??= new ProductRepository(_connection, () => _transaction, _options);

    public ITeamMemberRepository TeamMembers => _teamMembers ??= new TeamMemberRepository(_connection, () => _transaction, _options);

    public async Task BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress for this Unit of Work.");
        }

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
        }

        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(isolationLevel, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        try
        {
            await _transaction.CommitAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
        {
            return;
        }

        try
        {
            // ВАЖЛИВА ПАСТКА: Якщо збережена процедура кинула помилку з SET XACT_ABORT ON,
            // SQL Server автоматично завершує транзакцію і _transaction.Connection стає null.
            if (_transaction.Connection != null)
            {
                await _transaction.RollbackAsync(ct);
            }
        }
        catch (InvalidOperationException)
        {
            // Транзакція вже була відкочена або завершена на боці сервера — не маскуємо початковий виняток
        }
        catch (Exception)
        {
            // Ігноруємо помилки відкату від уже закритої транзакції
        }
        finally
        {
            try
            {
                await _transaction.DisposeAsync();
            }
            catch
            {
                // no-op
            }
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            try
            {
                await RollbackAsync();
            }
            catch
            {
                // no-op
            }
        }

        if (_ownsConnection)
        {
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}

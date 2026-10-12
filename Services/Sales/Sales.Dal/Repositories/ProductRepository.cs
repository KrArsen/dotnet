using Dapper;
using Microsoft.Data.SqlClient;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Domain.Models;

namespace Sales.Dal.Repositories;

public class ProductRepository : BaseDapperRepository<Product>, IProductRepository
{
    private const string Table = "dbo.Products";
    private const string IdCol = "Id";
    private const string SelectCols = "Id, Name, Price, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy";
    private const string InsertCols = "Id, Name, Price, IsActive, CreatedAt, CreatedBy";
    private const string InsertVals = "@Id, @Name, @Price, @IsActive, SYSUTCDATETIME(), @CreatedBy";
    private const string UpdateCols = "Name = @Name, Price = @Price, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy";

    public ProductRepository(
        SqlConnection connection,
        Func<SqlTransaction?> transactionProvider,
        DalOptions? options = null)
        : base(
            connection,
            transactionProvider,
            Table,
            IdCol,
            SelectCols,
            InsertCols,
            InsertVals,
            UpdateCols,
            hasSoftDelete: false,
            options)
    {
    }

    /// <summary>
    /// Специфічний Dapper-запит: пошук активних товарів/послуг за назвою з екрануванням спецсимволів LIKE.
    /// </summary>
    public async Task<IReadOnlyList<Product>> GetActiveByNameAsync(string name, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        const string sql = "SELECT Id, Name, Price, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy " +
                           "FROM dbo.Products WHERE IsActive = 1 AND Name LIKE @Pattern ORDER BY Name ASC;";

        var escaped = name.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

        var cmd = new CommandDefinition(
            sql,
            new { Pattern = $"%{escaped}%" },
            transaction: Transaction,
            commandTimeout: CommandTimeout,
            cancellationToken: ct);

        var items = await Connection.QueryAsync<Product>(cmd);
        return items.AsList();
    }

    /// <summary>
    /// Прямий запис у Products заборонено: таблиця є локальною копією Каталогу (CatalogDb).
    /// Синхронізація дозволена лише через події від сервісу Catalog для уникнення Dual-Write.
    /// </summary>
    public override Task<long> AddAsync(Product entity, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Прямий запис у Products заборонено: таблиця є локальною копією Каталогу (CatalogDb). Синхронізація дозволена лише через доменні події.");
    }

    /// <summary>
    /// Пряме оновлення Products заборонено: таблиця є локальною копією Каталогу (CatalogDb).
    /// </summary>
    public override Task<bool> UpdateAsync(Product entity, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Пряме оновлення Products заборонено: таблиця є локальною копією Каталогу (CatalogDb). Синхронізація дозволена лише через доменні події.");
    }

    /// <summary>
    /// Пряме видалення Products заборонено: таблиця є локальною копією Каталогу (CatalogDb).
    /// </summary>
    public override Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Пряме видалення Products заборонено: таблиця є локальною копією Каталогу (CatalogDb). Синхронізація дозволена лише через доменні події.");
    }
}

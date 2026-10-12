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
}

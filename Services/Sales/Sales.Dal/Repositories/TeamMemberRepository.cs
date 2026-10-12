using Dapper;
using Microsoft.Data.SqlClient;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Domain.Models;

namespace Sales.Dal.Repositories;

public class TeamMemberRepository : BaseDapperRepository<TeamMember>, ITeamMemberRepository
{
    private const string Table = "dbo.TeamMembers";
    private const string IdCol = "Id";
    private const string SelectCols = "Id, FullName, Email, Role, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted";
    private const string InsertCols = "FullName, Email, Role, IsActive, CreatedAt, CreatedBy, IsDeleted";
    private const string InsertVals = "@FullName, @Email, @Role, @IsActive, SYSUTCDATETIME(), @CreatedBy, 0";
    private const string UpdateCols = "FullName = @FullName, Email = @Email, Role = @Role, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy";

    public TeamMemberRepository(
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
            hasSoftDelete: true,
            options)
    {
    }

    /// <summary>
    /// Специфічний Dapper-запит: пошук активного співробітника за Email
    /// </summary>
    public async Task<TeamMember?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        const string sql = "SELECT Id, FullName, Email, Role, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted " +
                           "FROM dbo.TeamMembers WHERE Email = @Email AND IsDeleted = 0;";

        var cmd = new CommandDefinition(
            sql,
            new { Email = email },
            transaction: Transaction,
            commandTimeout: CommandTimeout,
            cancellationToken: ct);

        return await Connection.QuerySingleOrDefaultAsync<TeamMember>(cmd);
    }
}

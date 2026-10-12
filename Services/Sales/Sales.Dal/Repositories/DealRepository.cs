using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Sales.Dal.Helpers;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Domain.Exceptions;
using Sales.Domain.Models;

namespace Sales.Dal.Repositories;

public class DealRepository : BaseDapperRepository<Deal>, IDealRepository
{
    private const string Table = "dbo.Deals";
    private const string IdCol = "Id";
    private const string SelectCols = "Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion";
    private const string InsertCols = "DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted";
    private const string InsertVals = "@DealNumber, @Title, @ClientId, @OwnerId, @Stage, @TotalAmount, SYSUTCDATETIME(), @CreatedBy, 0";
    private const string UpdateCols = "Title = @Title, OwnerId = @OwnerId, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy";

    public DealRepository(
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
    /// Отримання повної інформації про угоду з деталями, позиціями та історією
    /// через збережену процедуру usp_GetDealById (QueryMultipleAsync з 3 результуючими наборами).
    /// </summary>
    public async Task<Deal?> GetWithDetailsAsync(long id, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var cmd = new CommandDefinition(
                "dbo.usp_GetDealById",
                new { DealId = id },
                transaction: Transaction,
                commandType: CommandType.StoredProcedure,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            await using var multi = await Connection.QueryMultipleAsync(cmd);

            // Набір 1: Загальна інформація про угоду, клієнта, менеджера та деталі
            var header = await multi.ReadSingleOrDefaultAsync<dynamic>();
            if (header == null)
            {
                return null;
            }

            var deal = new Deal
            {
                Id = (long)header.DealId,
                DealNumber = (string)header.DealNumber,
                Title = (string)header.Title,
                Stage = (string)header.Stage,
                TotalAmount = (decimal)header.TotalAmount,
                ClientId = (long)header.ClientId,
                OwnerId = (long)header.OwnerId,
                CreatedAt = (DateTime)header.CreatedAt,
                UpdatedAt = header.UpdatedAt != null ? (DateTime?)header.UpdatedAt : null,
                Details = new DealDetails
                {
                    DealId = (long)header.DealId,
                    Source = (string)header.Source,
                    Description = header.Description != null ? (string?)header.Description : null,
                    LostReason = header.LostReason != null ? (string?)header.LostReason : null,
                    ExpectedCloseDate = header.ExpectedCloseDate != null ? (DateTime?)header.ExpectedCloseDate : null
                },
                Client = new Client
                {
                    Id = (long)header.ClientId,
                    CompanyName = (string)header.CompanyName,
                    ContactPerson = (string)header.ContactPerson,
                    Email = (string)header.ClientEmail,
                    Phone = (string)header.ClientPhone
                },
                Owner = new TeamMember
                {
                    Id = (long)header.OwnerId,
                    FullName = (string)header.OwnerName,
                    Email = (string)header.OwnerEmail
                }
            };

            // Набір 2: Позиції угоди із незмінними знімками цін
            var items = await multi.ReadAsync<DealItem>();
            deal.Items = items.AsList();

            // Набір 3: Хронологічний аудит переходів по етапах воронки
            var history = await multi.ReadAsync<DealStatusHistoryEntry>();
            deal.History = history.AsList();

            return deal;
        });
    }

    /// <summary>
    /// Отримання сторінки зведених даних угод через представлення vw_DealSummary
    /// з параметризованою фільтрацією та білим списком сортування.
    /// </summary>
    public async Task<(IReadOnlyList<DealSummary> Items, int TotalCount)> GetPageAsync(
        string? status,
        long? ownerId,
        long? clientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var whereClauses = new List<string>();
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(status))
            {
                whereClauses.Add("Stage = @Stage");
                parameters.Add("@Stage", status);
            }

            if (ownerId.HasValue)
            {
                whereClauses.Add("OwnerId = @OwnerId");
                parameters.Add("@OwnerId", ownerId.Value);
            }

            if (clientId.HasValue)
            {
                whereClauses.Add("ClientId = @ClientId");
                parameters.Add("@ClientId", clientId.Value);
            }

            var whereSql = whereClauses.Count > 0
                ? "WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            var countSql = $"SELECT COUNT(1) FROM dbo.vw_DealSummary {whereSql};";

            var countCmd = new CommandDefinition(
                countSql,
                parameters,
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            var totalCount = await Connection.ExecuteScalarAsync<int>(countCmd);
            if (totalCount == 0)
            {
                return ((IReadOnlyList<DealSummary>)Array.Empty<DealSummary>(), 0);
            }

            parameters.Add("@Offset", (page - 1) * pageSize);
            parameters.Add("@PageSize", pageSize);

            var querySql = $@"
                SELECT 
                    DealId, DealNumber, Title, Stage, TotalAmount,
                    ClientId, ClientCompanyName, ClientContactPerson,
                    OwnerId, OwnerName, DealSource, ExpectedCloseDate, LostReason,
                    CreatedAt, UpdatedAt, ItemsCount, CalculatedTotal
                FROM dbo.vw_DealSummary
                {whereSql}
                ORDER BY DealId DESC
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;";

            var queryCmd = new CommandDefinition(
                querySql,
                parameters,
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            var items = await Connection.QueryAsync<DealSummary>(queryCmd);
            return (items.AsList(), totalCount);
        });
    }

    /// <summary>
    /// Транзакційне створення нової угоди через збережену процедуру usp_CreateDeal.
    /// Передає позиції як серіалізований JSON-параметр (OPENJSON у T-SQL).
    /// </summary>
    public async Task<long> CreateAsync(
        Deal deal,
        DealDetails details,
        IEnumerable<DealItem> items,
        CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            string? itemsJson = null;
            if (items != null && items.Any())
            {
                var payload = items.Select(i => new
                {
                    productId = i.ProductId,
                    quantity = i.Quantity,
                    discount = i.Discount
                });
                itemsJson = JsonSerializer.Serialize(payload);
            }

            var p = new DynamicParameters();
            p.Add("@ClientId", deal.ClientId);
            p.Add("@OwnerId", deal.OwnerId);
            p.Add("@Title", deal.Title);
            p.Add("@Source", string.IsNullOrWhiteSpace(details.Source) ? "Inbound" : details.Source);
            p.Add("@Description", details.Description);
            p.Add("@ExpectedCloseDate", details.ExpectedCloseDate);
            p.Add("@ItemsJson", itemsJson);
            p.Add("@CreatedBy", string.IsNullOrWhiteSpace(deal.CreatedBy) ? "system" : deal.CreatedBy);
            p.Add("@DealNumber", string.IsNullOrWhiteSpace(deal.DealNumber) ? null : deal.DealNumber);
            p.Add("@NewDealId", dbType: DbType.Int64, direction: ParameterDirection.Output);

            var cmd = new CommandDefinition(
                "dbo.usp_CreateDeal",
                p,
                transaction: Transaction,
                commandType: CommandType.StoredProcedure,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            await Connection.ExecuteAsync(cmd);
            var newId = p.Get<long>("@NewDealId");
            deal.Id = newId;
            return newId;
        });
    }

    /// <summary>
    /// Додавання позиції у відкриту угоду через збережену процедуру usp_AddDealItem зі знімком цін.
    /// </summary>
    public async Task<bool> AddItemAsync(
        long dealId,
        long productId,
        int quantity,
        decimal discount,
        string addedBy,
        CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var p = new DynamicParameters();
            p.Add("@DealId", dealId);
            p.Add("@ProductId", productId);
            p.Add("@Quantity", quantity);
            p.Add("@Discount", discount);
            p.Add("@AddedBy", string.IsNullOrWhiteSpace(addedBy) ? "system" : addedBy);

            var cmd = new CommandDefinition(
                "dbo.usp_AddDealItem",
                p,
                transaction: Transaction,
                commandType: CommandType.StoredProcedure,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            await Connection.ExecuteAsync(cmd);
            return true;
        });
    }

    /// <summary>
    /// Послідовний рух угоди на 1 етап вперед через процедуру usp_AdvanceDealStage.
    /// </summary>
    public async Task<bool> AdvanceAsync(
        long dealId,
        string changedBy,
        string? reason,
        CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var p = new DynamicParameters();
            p.Add("@DealId", dealId);
            p.Add("@ChangedBy", string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy);
            p.Add("@Reason", reason);

            var cmd = new CommandDefinition(
                "dbo.usp_AdvanceDealStage",
                p,
                transaction: Transaction,
                commandType: CommandType.StoredProcedure,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            await Connection.ExecuteAsync(cmd);
            return true;
        });
    }

    /// <summary>
    /// Фіналізація виграної угоди через збережену процедуру usp_WinDeal (Negotiation -> Won).
    /// </summary>
    public async Task<bool> WinAsync(
        long dealId,
        string changedBy,
        string reason,
        CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var p = new DynamicParameters();
            p.Add("@DealId", dealId);
            p.Add("@ChangedBy", string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy);
            p.Add("@Reason", string.IsNullOrWhiteSpace(reason) ? "Успішне підписання договору з клієнтом" : reason);

            var cmd = new CommandDefinition(
                "dbo.usp_WinDeal",
                p,
                transaction: Transaction,
                commandType: CommandType.StoredProcedure,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            await Connection.ExecuteAsync(cmd);
            return true;
        });
    }

    /// <summary>
    /// Закриття угоди з програшем через збережену процедуру usp_LoseDeal (з обов'язковою причиною).
    /// </summary>
    public async Task<bool> LoseAsync(
        long dealId,
        string reason,
        string changedBy,
        CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            var p = new DynamicParameters();
            p.Add("@DealId", dealId);
            p.Add("@Reason", reason);
            p.Add("@ChangedBy", string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy);

            var cmd = new CommandDefinition(
                "dbo.usp_LoseDeal",
                p,
                transaction: Transaction,
                commandType: CommandType.StoredProcedure,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            await Connection.ExecuteAsync(cmd);
            return true;
        });
    }

    /// <summary>
    /// Аналітична статистика кількості та сум угод за етапами воронки.
    /// </summary>
    public async Task<IReadOnlyList<DealStatusStat>> GetStatsByStatusAsync(CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            const string sql = @"
                SELECT 
                    d.Stage,
                    COUNT(d.Id) AS DealsCount,
                    ISNULL(SUM(d.TotalAmount), 0.00) AS TotalAmount,
                    CAST(AVG(d.TotalAmount) AS DECIMAL(18,2)) AS AverageAmount
                FROM dbo.Deals d
                WHERE d.IsDeleted = 0
                GROUP BY d.Stage
                ORDER BY 
                    CASE d.Stage
                        WHEN N'Lead' THEN 1
                        WHEN N'Qualified' THEN 2
                        WHEN N'ProposalSent' THEN 3
                        WHEN N'Negotiation' THEN 4
                        WHEN N'Won' THEN 5
                        WHEN N'Lost' THEN 6
                        ELSE 7
                    END;";

            var cmd = new CommandDefinition(
                sql,
                transaction: Transaction,
                commandTimeout: CommandTimeout,
                cancellationToken: ct);

            var result = await Connection.QueryAsync<DealStatusStat>(cmd);
            return result.AsList();
        });
    }
}

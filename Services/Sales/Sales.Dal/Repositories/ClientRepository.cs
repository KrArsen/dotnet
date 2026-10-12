using System.Data;
using Microsoft.Data.SqlClient;
using Sales.Dal.Helpers;
using Sales.Dal.Interfaces;
using Sales.Dal.Options;
using Sales.Domain.Exceptions;
using Sales.Domain.Models;

namespace Sales.Dal.Repositories;

/// <summary>
/// Репозиторій клієнтів, реалізований на чистому ADO.NET без Dapper.
/// Свідомо не успадковує Generic Repository. Використовує параметризовані SqlCommand,
/// явне керування SqlDbType, та контроль RowVersion для оптимістичного блокування.
/// </summary>
public class ClientRepository : RepositoryBase, IClientRepository
{
    public ClientRepository(
        SqlConnection connection,
        Func<SqlTransaction?> transactionProvider,
        DalOptions? options = null)
        : base(connection, transactionProvider, options)
    {
    }

    public async Task<Client?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            const string sql = @"
                SELECT Id, CompanyName, ContactPerson, Email, Phone, Industry,
                       CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
                FROM dbo.Clients
                WHERE Id = @Id AND IsDeleted = 0;";

            await using var cmd = new SqlCommand(sql, Connection, Transaction)
            {
                CommandTimeout = CommandTimeout
            };
            cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return MapClient(reader);
            }

            return null;
        });
    }

    public async Task<(IReadOnlyList<Client> Items, int TotalCount)> GetPageAsync(
        string? search,
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

            const string countSql = @"
                SELECT COUNT(1)
                FROM dbo.Clients
                WHERE IsDeleted = 0
                  AND (@Pattern IS NULL 
                       OR CompanyName LIKE @Pattern 
                       OR ContactPerson LIKE @Pattern 
                       OR Email LIKE @Pattern);";

            int totalCount;
            await using (var countCmd = new SqlCommand(countSql, Connection, Transaction))
            {
                countCmd.CommandTimeout = CommandTimeout;
                var patternParam = countCmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 200);
                patternParam.Value = string.IsNullOrWhiteSpace(search) 
                    ? DBNull.Value 
                    : $"%{EscapeLikePattern(search)}%";

                var scalar = await countCmd.ExecuteScalarAsync(ct);
                totalCount = Convert.ToInt32(scalar);
            }

            if (totalCount == 0)
            {
                return ((IReadOnlyList<Client>)Array.Empty<Client>(), 0);
            }

            const string querySql = @"
                SELECT Id, CompanyName, ContactPerson, Email, Phone, Industry,
                       CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
                FROM dbo.Clients
                WHERE IsDeleted = 0
                  AND (@Pattern IS NULL 
                       OR CompanyName LIKE @Pattern 
                       OR ContactPerson LIKE @Pattern 
                       OR Email LIKE @Pattern)
                ORDER BY CompanyName ASC
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;";

            var items = new List<Client>();
            await using (var queryCmd = new SqlCommand(querySql, Connection, Transaction))
            {
                queryCmd.CommandTimeout = CommandTimeout;
                var patternParam = queryCmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 200);
                patternParam.Value = string.IsNullOrWhiteSpace(search) 
                    ? DBNull.Value 
                    : $"%{EscapeLikePattern(search)}%";

                queryCmd.Parameters.Add("@Offset", SqlDbType.Int).Value = (page - 1) * pageSize;
                queryCmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

                await using var reader = await queryCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    items.Add(MapClient(reader));
                }
            }

            return ((IReadOnlyList<Client>)items, totalCount);
        });
    }

    public async Task<long> AddAsync(Client client, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            const string sql = @"
                INSERT INTO dbo.Clients 
                    (CompanyName, ContactPerson, Email, Phone, Industry, CreatedAt, CreatedBy, IsDeleted)
                OUTPUT INSERTED.Id
                VALUES 
                    (@CompanyName, @ContactPerson, @Email, @Phone, @Industry, SYSUTCDATETIME(), @CreatedBy, 0);";

            await using var cmd = new SqlCommand(sql, Connection, Transaction)
            {
                CommandTimeout = CommandTimeout
            };
            cmd.Parameters.Add("@CompanyName", SqlDbType.NVarChar, 200).Value = client.CompanyName;
            cmd.Parameters.Add("@ContactPerson", SqlDbType.NVarChar, 150).Value = client.ContactPerson;
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = client.Email;
            cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 50).Value = client.Phone;
            cmd.Parameters.Add("@Industry", SqlDbType.NVarChar, 100).Value = (object?)client.Industry ?? DBNull.Value;
            cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(client.CreatedBy) ? "system" : client.CreatedBy;

            var insertedId = await cmd.ExecuteScalarAsync(ct);
            client.Id = Convert.ToInt64(insertedId);
            return client.Id;
        });
    }

    public async Task<bool> UpdateAsync(Client client, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            if (client.RowVersion == null || client.RowVersion.Length == 0)
            {
                throw new ValidationException("RowVersion is required for updating Client to prevent concurrency conflicts.");
            }

            const string sql = @"
                UPDATE dbo.Clients
                SET CompanyName = @CompanyName,
                    ContactPerson = @ContactPerson,
                    Email = @Email,
                    Phone = @Phone,
                    Industry = @Industry,
                    UpdatedAt = SYSUTCDATETIME(),
                    UpdatedBy = @UpdatedBy
                WHERE Id = @Id
                  AND RowVersion = @RowVersion
                  AND IsDeleted = 0;";

            await using var cmd = new SqlCommand(sql, Connection, Transaction)
            {
                CommandTimeout = CommandTimeout
            };
            cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = client.Id;
            cmd.Parameters.Add("@CompanyName", SqlDbType.NVarChar, 200).Value = client.CompanyName;
            cmd.Parameters.Add("@ContactPerson", SqlDbType.NVarChar, 150).Value = client.ContactPerson;
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = client.Email;
            cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 50).Value = client.Phone;
            cmd.Parameters.Add("@Industry", SqlDbType.NVarChar, 100).Value = (object?)client.Industry ?? DBNull.Value;
            cmd.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = (object?)client.UpdatedBy ?? "system";

            var rowVersionParam = cmd.Parameters.Add("@RowVersion", SqlDbType.Timestamp);
            rowVersionParam.Value = client.RowVersion;

            var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
            if (rowsAffected == 0)
            {
                throw new BusinessConflictException(
                    $"Concurrency conflict or record deleted: Client with ID '{client.Id}' was modified or deleted by another user.");
            }

            return true;
        });
    }

    public async Task<bool> SoftDeleteAsync(long id, string updatedBy, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            const string sql = @"
                UPDATE dbo.Clients
                SET IsDeleted = 1,
                    UpdatedAt = SYSUTCDATETIME(),
                    UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND IsDeleted = 0;";

            await using var cmd = new SqlCommand(sql, Connection, Transaction)
            {
                CommandTimeout = CommandTimeout
            };
            cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
            cmd.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy;

            var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
            return rowsAffected > 0;
        });
    }

    public async Task<bool> RegisterWonDealAsync(long clientId, string updatedBy, CancellationToken ct = default)
    {
        return await SqlExceptionTranslator.ExecuteAsync(async () =>
        {
            await EnsureOpenAsync(ct);

            const string sql = @"
                UPDATE dbo.Clients
                SET UpdatedAt = SYSUTCDATETIME(),
                    UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND IsDeleted = 0;";

            await using var cmd = new SqlCommand(sql, Connection, Transaction)
            {
                CommandTimeout = CommandTimeout
            };
            cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = clientId;
            cmd.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy;

            var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
            if (rowsAffected == 0)
            {
                throw new BusinessConflictException(
                    $"Cannot register won deal: Client with ID '{clientId}' does not exist or has been deleted.");
            }

            return true;
        });
    }

    private static Client MapClient(SqlDataReader reader)
    {
        return new Client
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
            ContactPerson = reader.GetString(reader.GetOrdinal("ContactPerson")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            Phone = reader.GetString(reader.GetOrdinal("Phone")),
            Industry = reader.IsDBNull(reader.GetOrdinal("Industry")) ? null : reader.GetString(reader.GetOrdinal("Industry")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
            UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetString(reader.GetOrdinal("UpdatedBy")),
            IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
            RowVersion = (byte[])reader["RowVersion"]
        };
    }

    private static string EscapeLikePattern(string input)
    {
        return input.Replace("[", "[[]")
                    .Replace("%", "[%]")
                    .Replace("_", "[_]");
    }
}

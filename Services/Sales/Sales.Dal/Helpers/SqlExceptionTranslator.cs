using Microsoft.Data.SqlClient;
using Sales.Domain.Exceptions;

namespace Sales.Dal.Helpers;

public static class SqlExceptionTranslator
{
    public static Exception Translate(SqlException sqlEx)
    {
        return sqlEx.Number switch
        {
            50001 => new NotFoundException(sqlEx.Message),
            50002 => new BusinessConflictException(sqlEx.Message),
            50003 => new BusinessConflictException(sqlEx.Message),
            50004 => new BusinessConflictException(string.IsNullOrWhiteSpace(sqlEx.Message) 
                ? "Product not found or inactive in catalog copy" 
                : sqlEx.Message),
            50005 => new ValidationException(sqlEx.Message),
            2601 or 2627 => new BusinessConflictException("Record with the same key already exists."),
            547 => new BusinessConflictException("Integrity constraint violation (foreign key or check constraint)."),
            1205 => new BusinessConflictException("The operation was deadlocked. Please retry your request."),
            _ => sqlEx
        };
    }

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SqlException ex)
        {
            var translated = Translate(ex);
            if (ReferenceEquals(translated, ex))
            {
                throw;
            }
            throw translated;
        }
    }

    public static async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (SqlException ex)
        {
            var translated = Translate(ex);
            if (ReferenceEquals(translated, ex))
            {
                throw;
            }
            throw translated;
        }
    }
}

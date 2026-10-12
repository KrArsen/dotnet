using System.Reflection;
using System.Runtime.Serialization;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Sales.Dal.Helpers;
using Sales.Domain.Exceptions;
using Xunit;

namespace Sales.Tests.Unit;

public class SqlExceptionTranslatorTests
{
    private static SqlException CreateSqlException(int errorNumber, string message = "SQL Error")
    {
        // Створення SqlException через reflection для юніт-тестування мапінгу помилок SQL Server
        var collectionConstructor = typeof(SqlErrorCollection).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
        var errorCollection = (SqlErrorCollection)collectionConstructor.Invoke(null);

        var errorConstructors = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        var errorConstructor = errorConstructors.OrderByDescending(c => c.GetParameters().Length).First();

        var parameters = errorConstructor.GetParameters();
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var pType = parameters[i].ParameterType;
            if (pType == typeof(int) && parameters[i].Name?.Equals("infoNumber", StringComparison.OrdinalIgnoreCase) == true)
                args[i] = errorNumber;
            else if (pType == typeof(string) && parameters[i].Name?.Equals("message", StringComparison.OrdinalIgnoreCase) == true)
                args[i] = message;
            else if (pType == typeof(string))
                args[i] = "test";
            else if (pType == typeof(byte))
                args[i] = (byte)1;
            else if (pType == typeof(int))
                args[i] = errorNumber;
            else
                args[i] = null;
        }

        var error = errorConstructor.Invoke(args);

        var addMethod = typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);
        addMethod?.Invoke(errorCollection, new[] { error });

        var exConstructor = typeof(SqlException).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var exParams = exConstructor.GetParameters();
        var exArgs = new object?[exParams.Length];
        for (int i = 0; i < exParams.Length; i++)
        {
            if (exParams[i].ParameterType == typeof(string))
                exArgs[i] = message;
            else if (exParams[i].ParameterType == typeof(SqlErrorCollection))
                exArgs[i] = errorCollection;
            else if (exParams[i].ParameterType == typeof(Exception))
                exArgs[i] = null;
            else if (exParams[i].ParameterType == typeof(Guid))
                exArgs[i] = Guid.NewGuid();
            else
                exArgs[i] = null;
        }

        return (SqlException)exConstructor.Invoke(exArgs);
    }

    [Fact]
    public void Translate_Error50001_ReturnsNotFoundException()
    {
        var sqlEx = CreateSqlException(50001, "Entity not found");
        var result = SqlExceptionTranslator.Translate(sqlEx);

        result.Should().BeOfType<NotFoundException>();
        result.Message.Should().Contain("Entity not found");
    }

    [Fact]
    public void Translate_Error50002_ReturnsBusinessConflictException()
    {
        var sqlEx = CreateSqlException(50002, "Invalid transition");
        var result = SqlExceptionTranslator.Translate(sqlEx);

        result.Should().BeOfType<BusinessConflictException>();
    }

    [Fact]
    public void Translate_Error50005_ReturnsValidationException()
    {
        var sqlEx = CreateSqlException(50005, "Validation failed");
        var result = SqlExceptionTranslator.Translate(sqlEx);

        result.Should().BeOfType<ValidationException>();
    }

    [Fact]
    public void Translate_UniqueConstraint2601_ReturnsBusinessConflictException()
    {
        var sqlEx = CreateSqlException(2601, "Cannot insert duplicate key");
        var result = SqlExceptionTranslator.Translate(sqlEx);

        result.Should().BeOfType<BusinessConflictException>()
            .Which.Message.Should().Contain("already exists");
    }

    [Fact]
    public void Translate_Deadlock1205_ReturnsBusinessConflictException()
    {
        var sqlEx = CreateSqlException(1205, "Deadlock victim");
        var result = SqlExceptionTranslator.Translate(sqlEx);

        result.Should().BeOfType<BusinessConflictException>()
            .Which.Message.Should().Contain("deadlocked");
    }
}

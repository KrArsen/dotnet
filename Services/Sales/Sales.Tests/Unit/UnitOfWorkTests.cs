using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Sales.Dal.UnitOfWork;
using Xunit;

namespace Sales.Tests.Unit;

public class UnitOfWorkTests
{
    [Fact]
    public async Task RollbackAsync_WhenNoActiveTransaction_DoesNotThrow()
    {
        // Arrange
        // Створюємо UnitOfWork з закритою Connection без відкритої транзакції
        using var connection = new SqlConnection("Server=dummy;Database=dummy;TrustServerCertificate=True;");
        var uow = new UnitOfWork(connection);

        // Act
        var act = async () => await uow.RollbackAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        using var connection = new SqlConnection("Server=dummy;Database=dummy;TrustServerCertificate=True;");
        var uow = new UnitOfWork(connection);

        // Act
        var act = async () =>
        {
            await uow.DisposeAsync();
            await uow.DisposeAsync();
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_WhenEmptyConnectionString_ThrowsArgumentException()
    {
        // Act
        var act = () => new UnitOfWork(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }
}

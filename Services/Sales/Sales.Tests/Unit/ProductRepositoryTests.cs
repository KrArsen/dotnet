using FluentAssertions;
using Microsoft.Data.SqlClient;
using Sales.Dal.Repositories;
using Sales.Domain.Models;
using Xunit;

namespace Sales.Tests.Unit;

public class ProductRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThrowsNotSupportedException_BecauseProductIsCatalogReplica()
    {
        // Arrange
        using var connection = new SqlConnection("Server=dummy;Database=dummy;TrustServerCertificate=True;");
        var repo = new ProductRepository(connection, () => null);

        // Act
        var act = async () => await repo.AddAsync(new Product { Name = "New Service", Price = 1000m });

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*локальною копією Каталогу*");
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotSupportedException_BecauseProductIsCatalogReplica()
    {
        // Arrange
        using var connection = new SqlConnection("Server=dummy;Database=dummy;TrustServerCertificate=True;");
        var repo = new ProductRepository(connection, () => null);

        // Act
        var act = async () => await repo.UpdateAsync(new Product { Id = 1, Name = "Updated Service", Price = 2000m });

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*локальною копією Каталогу*");
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotSupportedException_BecauseProductIsCatalogReplica()
    {
        // Arrange
        using var connection = new SqlConnection("Server=dummy;Database=dummy;TrustServerCertificate=True;");
        var repo = new ProductRepository(connection, () => null);

        // Act
        var act = async () => await repo.DeleteAsync(1L);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*локальною копією Каталогу*");
    }
}

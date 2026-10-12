using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Sales.Bll.Extensions;
using Xunit;

namespace Sales.Tests.Unit;

public class AutoMapperTests
{
    [Fact]
    public void AutoMapper_Configuration_ShouldBeValid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSalesBll();
        using var sp = services.BuildServiceProvider();

        // Act
        var mapper = sp.GetRequiredService<IMapper>();

        // Assert
        mapper.ConfigurationProvider.AssertConfigurationIsValid();
    }
}

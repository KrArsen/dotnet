using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sales.Api.Middleware;
using Sales.Bll.Dto;
using Sales.Bll.Services;
using Sales.Domain.Exceptions;
using Xunit;

namespace Sales.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IClientService _clientServiceMock = Substitute.For<IClientService>();
    private readonly IDealService _dealServiceMock = Substitute.For<IDealService>();

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Підміняємо бізнес-сервіси mock-об'єктами для детермінованого тестування HTTP-контракту,
                // статус-кодів (400, 404, 409), ProblemDetails та CorrelationId
                services.AddScoped(_ => _clientServiceMock);
                services.AddScoped(_ => _dealServiceMock);
            });
        });
    }

    [Fact]
    public async Task CorrelationId_Header_IsGeneratedAndReturnedWhenMissing()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/team-members");

        // Assert
        response.Headers.Contains(CorrelationIdMiddleware.CorrelationIdHeader).Should().BeTrue();
        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.CorrelationIdHeader).FirstOrDefault();
        correlationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CorrelationId_Header_IsPreservedWhenProvidedInRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        const string customId = "custom-test-correlation-12345";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/team-members");
        request.Headers.Add(CorrelationIdMiddleware.CorrelationIdHeader, customId);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Contains(CorrelationIdMiddleware.CorrelationIdHeader).Should().BeTrue();
        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.CorrelationIdHeader).FirstOrDefault();
        correlationId.Should().Be(customId);
    }

    [Fact]
    public async Task GetClient_WhenNotFound_Returns404WithProblemDetails()
    {
        // Arrange
        _clientServiceMock.GetByIdAsync(999999, Arg.Any<CancellationToken>())
            .Returns((ClientDto?)null);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/clients/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Ресурс не знайдено");
        problem.Detail.Should().Contain("999999");
        problem.Extensions.Should().ContainKey("correlationId");
    }

    [Fact]
    public async Task CreateClient_WithInvalidModel_Returns400WithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        var invalidPayload = new { CompanyName = "", Email = "not-an-email" };

        // Act
        var response = await client.PostAsJsonAsync("/api/clients", invalidPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("errors");
    }

    [Fact]
    public async Task WinDeal_WhenBusinessConflictOccurs_Returns409WithProblemDetails()
    {
        // Arrange
        const long dealId = 15;
        _dealServiceMock.WinDealAsync(dealId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => Task.FromException<bool>(new BusinessConflictException("Неможливо виграти угоду без позицій або якщо клієнта видалено.")));

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/api/deals/{dealId}/win", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Конфлікт бізнес-правил");
        problem.Detail.Should().Contain("Неможливо виграти угоду");
        problem.Extensions.Should().ContainKey("correlationId");
    }

    [Fact]
    public async Task SwaggerEndpoint_Returns200OK()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Sales Microservice API");
    }
}

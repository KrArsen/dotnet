using System.Data;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sales.Bll.Dto;
using Sales.Bll.Extensions;
using Sales.Bll.Services;
using Sales.Bll.Validation;
using Sales.Dal.Interfaces;
using Sales.Domain.Constants;
using Sales.Domain.Exceptions;
using Sales.Domain.Models;
using Xunit;

namespace Sales.Tests.Unit;

public class DealServiceTests
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IDealValidator _validator;
    private readonly DealService _sut;

    public DealServiceTests()
    {
        _uow = Substitute.For<IUnitOfWork>();
        var services = new ServiceCollection();
        services.AddSalesBll();
        var sp = services.BuildServiceProvider();
        _mapper = sp.GetRequiredService<IMapper>();
        _validator = Substitute.For<IDealValidator>();

        _sut = new DealService(_uow, _mapper, _validator);
    }

    [Fact]
    public async Task CreateAsync_ExecutesWithinTransaction_AndSnapshotsProductNameAndPrice()
    {
        // Arrange
        const long productId = 5;
        var product = new Product
        {
            Id = productId,
            Name = "SEO Оптимізація та аудит",
            Price = 15000m,
            IsActive = true
        };

        var dto = new CreateDealDto
        {
            ClientId = 10,
            OwnerId = 2,
            Title = "Проєкт просування в пошуку",
            Source = "Website",
            Items = new List<CreateDealItemDto>
            {
                new CreateDealItemDto
                {
                    ProductId = productId,
                    Quantity = 2,
                    Discount = 1000m
                }
            }
        };

        _uow.Products.GetByIdAsync(productId, Arg.Any<CancellationToken>()).Returns(product);
        _uow.Deals.CreateAsync(Arg.Any<Deal>(), Arg.Any<DealDetails>(), Arg.Any<IEnumerable<DealItem>>(), Arg.Any<CancellationToken>())
            .Returns(100L);

        var createdDeal = new Deal
        {
            Id = 100L,
            Title = dto.Title,
            ClientId = dto.ClientId,
            OwnerId = dto.OwnerId,
            Stage = DealStatuses.Lead,
            TotalAmount = 29000m,
            Items = new List<DealItem>
            {
                new DealItem
                {
                    DealId = 100L,
                    ProductId = productId,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = 2,
                    Discount = 1000m
                }
            }
        };

        _uow.Deals.GetWithDetailsAsync(100L, Arg.Any<CancellationToken>()).Returns(createdDeal);

        // Act
        var result = await _sut.CreateAsync(dto, "manager@studio.ua");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(100L);
        result.Items.Should().HaveCount(1);
        result.Items[0].ProductName.Should().Be("SEO Оптимізація та аудит");
        result.Items[0].UnitPrice.Should().Be(15000m);

        await _uow.Received(1).BeginTransactionAsync(IsolationLevel.ReadCommitted, Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WinDealAsync_WhenBothRepositoriesSucceed_CommitsTransaction()
    {
        // Arrange
        const long dealId = 42;
        const long clientId = 10;
        var deal = new Deal
        {
            Id = dealId,
            ClientId = clientId,
            OwnerId = 1,
            Stage = DealStatuses.Negotiation,
            TotalAmount = 50000m,
            IsDeleted = false,
            Items = new List<DealItem>
            {
                new DealItem
                {
                    DealId = dealId,
                    ProductId = 1,
                    UnitPrice = 50000m,
                    Quantity = 1,
                    Discount = 0m
                }
            }
        };

        _uow.Deals.GetWithDetailsAsync(dealId, Arg.Any<CancellationToken>()).Returns(deal);
        _uow.Deals.WinAsync(dealId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _uow.Clients.RegisterWonDealAsync(clientId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.WinDealAsync(dealId, "manager@studio.ua");

        // Assert
        result.Should().BeTrue();
        await _uow.Received(1).BeginTransactionAsync(IsolationLevel.ReadCommitted, Arg.Any<CancellationToken>());
        await _uow.Deals.Received(1).WinAsync(dealId, "manager@studio.ua", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _uow.Clients.Received(1).RegisterWonDealAsync(clientId, "manager@studio.ua", Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WinDealAsync_WhenSecondRepositoryFails_RollsBackTransactionAndThrows()
    {
        // Arrange
        const long dealId = 42;
        const long clientId = 10;
        var deal = new Deal
        {
            Id = dealId,
            ClientId = clientId,
            OwnerId = 1,
            Stage = DealStatuses.Negotiation,
            TotalAmount = 50000m,
            IsDeleted = false,
            Items = new List<DealItem>
            {
                new DealItem
                {
                    DealId = dealId,
                    ProductId = 1,
                    UnitPrice = 50000m,
                    Quantity = 1,
                    Discount = 0m
                }
            }
        };

        _uow.Deals.GetWithDetailsAsync(dealId, Arg.Any<CancellationToken>()).Returns(deal);
        _uow.Deals.WinAsync(dealId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Імітуємо падіння другого репозиторію (клієнт видалений або конфлікт версій в ADO.NET)
        _uow.Clients.RegisterWonDealAsync(clientId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => Task.FromException<bool>(new BusinessConflictException("Неможливо зареєструвати виграну угоду: клієнта з ID 10 видалено.")));

        // Act
        var act = async () => await _sut.WinDealAsync(dealId, "manager@studio.ua");

        // Assert
        await act.Should().ThrowAsync<BusinessConflictException>()
            .WithMessage("*клієнта з ID 10 видалено*");

        await _uow.Received(1).BeginTransactionAsync(IsolationLevel.ReadCommitted, Arg.Any<CancellationToken>());
        await _uow.Deals.Received(1).WinAsync(dealId, "manager@studio.ua", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _uow.Clients.Received(1).RegisterWonDealAsync(clientId, "manager@studio.ua", Arg.Any<CancellationToken>());
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WinDealAsync_WhenDealHasNoItems_ThrowsBusinessConflictAndRollsBack()
    {
        // Arrange
        const long dealId = 42;
        var deal = new Deal
        {
            Id = dealId,
            ClientId = 10,
            OwnerId = 1,
            Stage = DealStatuses.Negotiation,
            TotalAmount = 0m,
            IsDeleted = false,
            Items = new List<DealItem>() // порожньо!
        };

        _uow.Deals.GetWithDetailsAsync(dealId, Arg.Any<CancellationToken>()).Returns(deal);

        // Act
        var act = async () => await _sut.WinDealAsync(dealId, "manager@studio.ua");

        // Assert
        await act.Should().ThrowAsync<BusinessConflictException>()
            .WithMessage("*без доданих товарів/послуг*");

        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WinDealAsync_WhenDealNotFound_ThrowsNotFoundExceptionAndRollsBack()
    {
        // Arrange
        const long dealId = 999;
        _uow.Deals.GetWithDetailsAsync(dealId, Arg.Any<CancellationToken>()).Returns((Deal?)null);

        // Act
        var act = async () => await _sut.WinDealAsync(dealId, "manager@studio.ua");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }
}

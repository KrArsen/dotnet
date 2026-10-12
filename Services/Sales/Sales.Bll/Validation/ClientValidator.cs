using Sales.Bll.Dto;
using Sales.Dal.Interfaces;
using Sales.Domain.Exceptions;

namespace Sales.Bll.Validation;

public interface IClientValidator
{
    Task ValidateCreateAsync(CreateClientDto dto, CancellationToken ct = default);
    Task ValidateUpdateAsync(long id, UpdateClientDto dto, CancellationToken ct = default);
}

public class ClientValidator : IClientValidator
{
    private readonly IUnitOfWork _uow;

    public ClientValidator(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public Task ValidateCreateAsync(CreateClientDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CompanyName))
        {
            throw new ValidationException("CompanyName cannot be empty.");
        }

        return Task.CompletedTask;
    }

    public Task ValidateUpdateAsync(long id, UpdateClientDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RowVersion))
        {
            throw new ValidationException("RowVersion is required for optimistic concurrency verification.");
        }

        return Task.CompletedTask;
    }
}

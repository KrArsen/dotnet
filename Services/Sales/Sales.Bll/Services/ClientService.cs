using AutoMapper;
using Sales.Bll.Dto;
using Sales.Bll.Validation;
using Sales.Dal.Interfaces;
using Sales.Domain.Exceptions;
using Sales.Domain.Models;

namespace Sales.Bll.Services;

public interface IClientService
{
    Task<ClientDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PagedResult<ClientDto>> GetPageAsync(ClientQueryDto query, CancellationToken ct = default);
    Task<ClientDto> CreateAsync(CreateClientDto dto, string createdBy, CancellationToken ct = default);
    Task<ClientDto> UpdateAsync(long id, UpdateClientDto dto, string updatedBy, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(long id, string updatedBy, CancellationToken ct = default);
}

public class ClientService : IClientService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IClientValidator _validator;
    private readonly Microsoft.Extensions.Logging.ILogger<ClientService>? _logger;

    public ClientService(
        IUnitOfWork uow,
        IMapper mapper,
        IClientValidator validator,
        Microsoft.Extensions.Logging.ILogger<ClientService>? logger = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger;
    }

    public async Task<ClientDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var client = await _uow.Clients.GetByIdAsync(id, ct);
        return client == null ? null : _mapper.Map<ClientDto>(client);
    }

    public async Task<PagedResult<ClientDto>> GetPageAsync(ClientQueryDto query, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Clients.GetPageAsync(query.Search, query.Page, query.PageSize, ct);
        var dtos = _mapper.Map<IReadOnlyList<ClientDto>>(items);
        return new PagedResult<ClientDto>(dtos, total, query.Page, query.PageSize);
    }

    public async Task<ClientDto> CreateAsync(CreateClientDto dto, string createdBy, CancellationToken ct = default)
    {
        await _validator.ValidateCreateAsync(dto, ct);

        var client = _mapper.Map<Client>(dto);
        client.CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy;

        var id = await _uow.Clients.AddAsync(client, ct);
        var created = await _uow.Clients.GetByIdAsync(id, ct);

        if (created == null)
        {
            throw new InvalidOperationException("Failed to retrieve created client.");
        }

        return _mapper.Map<ClientDto>(created);
    }

    public async Task<ClientDto> UpdateAsync(long id, UpdateClientDto dto, string updatedBy, CancellationToken ct = default)
    {
        await _validator.ValidateUpdateAsync(id, dto, ct);

        var existing = await _uow.Clients.GetByIdAsync(id, ct);
        if (existing == null || existing.IsDeleted)
        {
            throw new NotFoundException("Client", id);
        }

        var clientToUpdate = _mapper.Map<Client>(dto);
        clientToUpdate.Id = id;
        clientToUpdate.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy;

        await _uow.Clients.UpdateAsync(clientToUpdate, ct);

        var updated = await _uow.Clients.GetByIdAsync(id, ct);
        if (updated == null)
        {
            throw new InvalidOperationException("Failed to retrieve updated client.");
        }

        return _mapper.Map<ClientDto>(updated);
    }

    public async Task<bool> SoftDeleteAsync(long id, string updatedBy, CancellationToken ct = default)
    {
        var existing = await _uow.Clients.GetByIdAsync(id, ct);
        if (existing == null || existing.IsDeleted)
        {
            throw new NotFoundException("Client", id);
        }

        return await _uow.Clients.SoftDeleteAsync(id, updatedBy, ct);
    }
}

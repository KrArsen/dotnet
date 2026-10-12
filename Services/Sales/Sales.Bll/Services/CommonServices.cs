using AutoMapper;
using Sales.Bll.Dto;
using Sales.Dal.Interfaces;

namespace Sales.Bll.Services;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetActiveByNameAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// Сервіс товарів/послуг. У мікросервісі Sales надає операції виключно на ЧИТАННЯ,
/// оскільки таблиця Products у SalesDb є локальною реплікою Каталогу (CatalogDb).
/// Модифікація дозволена лише через доменні події від Каталогу задля запобігання Dual-Write.
/// </summary>
public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<ProductDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct);
        return product == null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        var products = await _uow.Products.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<ProductDto>>(products);
    }

    public async Task<IReadOnlyList<ProductDto>> GetActiveByNameAsync(string name, CancellationToken ct = default)
    {
        var products = await _uow.Products.GetActiveByNameAsync(name, ct);
        return _mapper.Map<IReadOnlyList<ProductDto>>(products);
    }
}

public interface ITeamMemberService
{
    Task<TeamMemberDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<TeamMemberDto>> GetAllAsync(CancellationToken ct = default);
    Task<TeamMemberDto?> GetByEmailAsync(string email, CancellationToken ct = default);
}

public class TeamMemberService : ITeamMemberService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public TeamMemberService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<TeamMemberDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var member = await _uow.TeamMembers.GetByIdAsync(id, ct);
        return member == null ? null : _mapper.Map<TeamMemberDto>(member);
    }

    public async Task<IReadOnlyList<TeamMemberDto>> GetAllAsync(CancellationToken ct = default)
    {
        var members = await _uow.TeamMembers.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<TeamMemberDto>>(members);
    }

    public async Task<TeamMemberDto?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var member = await _uow.TeamMembers.GetByEmailAsync(email, ct);
        return member == null ? null : _mapper.Map<TeamMemberDto>(member);
    }
}

using System.Data;

namespace Sales.Dal.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    IClientRepository Clients { get; }
    IDealRepository Deals { get; }
    IProductRepository Products { get; }
    ITeamMemberRepository TeamMembers { get; }

    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

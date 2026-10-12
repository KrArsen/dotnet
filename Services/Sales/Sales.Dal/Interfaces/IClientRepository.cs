using Sales.Domain.Models;

namespace Sales.Dal.Interfaces;

/// <summary>
/// Репозиторій клієнтів на чистому ADO.NET.
/// Свідомо НЕ успадковує IGenericRepository, оскільки вимагає суворого управління
/// оптимістичним блокуванням (RowVersion), спеціалізованої пагінації та реєстрації виграних угод.
/// </summary>
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<Client> Items, int TotalCount)> GetPageAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<long> AddAsync(Client client, CancellationToken ct = default);
    Task<bool> UpdateAsync(Client client, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(long id, string updatedBy, CancellationToken ct = default);
    Task<bool> RegisterWonDealAsync(long clientId, string updatedBy, CancellationToken ct = default);
}

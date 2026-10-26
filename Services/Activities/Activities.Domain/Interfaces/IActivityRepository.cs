using Activities.Domain.Entities;
using MongoDB.Bson;

namespace Activities.Domain.Interfaces;

/// <summary>
/// Інтерфейс репозиторію для агрегату Activity
/// </summary>
public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(ObjectId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(long clientId, long userId, CancellationToken ct = default);
    Task AddAsync(Activity entity, CancellationToken ct = default);
    Task UpdateAsync(Activity entity, int expectedVersion, CancellationToken ct = default);
    Task DeleteAsync(ObjectId id, CancellationToken ct = default);
}

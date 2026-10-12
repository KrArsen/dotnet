using Sales.Domain.Models;

namespace Sales.Dal.Interfaces;

public interface ITeamMemberRepository : IGenericRepository<TeamMember>
{
    /// <summary>
    /// Специфічний Dapper-запит: пошук співробітника за унікальною електронною поштою
    /// </summary>
    Task<TeamMember?> GetByEmailAsync(string email, CancellationToken ct = default);
}

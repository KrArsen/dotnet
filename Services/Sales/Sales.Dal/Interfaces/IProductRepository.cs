using Sales.Domain.Models;

namespace Sales.Dal.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>
    /// Пошук активних товарів/послуг за назвою з екрануванням спецсимволів LIKE
    /// </summary>
    Task<IReadOnlyList<Product>> GetActiveByNameAsync(string name, CancellationToken ct = default);
}

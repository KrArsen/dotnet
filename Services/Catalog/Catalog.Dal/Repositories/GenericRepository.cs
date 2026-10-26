using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Catalog.Dal.Context;
using Catalog.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public GenericRepository(AppDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync(new[] { id }, ct);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().ToListAsync(ct);
    }

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await DbSet.AddAsync(entity, ct);
    }

    public virtual void Update(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbSet.Update(entity);
    }

    public virtual void Delete(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbSet.Remove(entity);
    }

    public virtual async Task<T?> GetBySpecAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync(ct);
    }

    public virtual async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        return await ApplySpecification(spec).ToListAsync(ct);
    }

    public virtual async Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        return await ApplySpecification(spec, true).CountAsync(ct);
    }

    protected IQueryable<T> ApplySpecification(ISpecification<T> spec, bool evaluateCriteriaOnly = false)
    {
        return SpecificationEvaluator.Default.GetQuery(DbSet.AsQueryable(), spec, evaluateCriteriaOnly);
    }
}

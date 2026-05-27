using System.Linq.Expressions;

namespace DataAccessLayer.Repositories;

/// <summary>
/// Generic repository interface providing standard CRUD operations.
/// Use <see cref="Query"/> to compose additional EF Core operations
/// such as Include, Where, OrderBy before materializing.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();

    Task<T?> GetByIdAsync(int id);

    Task AddAsync(T entity);

    void Update(T entity);

    void Delete(T entity);

    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Returns an <see cref="IQueryable{T}"/> for advanced query composition
    /// (Include, Where, OrderBy, etc.) before materializing with ToListAsync.
    /// </summary>
    IQueryable<T> Query();
}

using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace DataAccessLayer.UnitOfWork;

/// <summary>
/// Coordinates multiple repositories sharing a single DbContext.
/// Call <see cref="SaveChangesAsync"/> once to commit all changes atomically.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<Category> Categories { get; }

    IRepository<Product> Products { get; }

    Task<int> SaveChangesAsync();
}

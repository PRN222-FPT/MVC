using DataAccessLayer.Models;

namespace DataAccessLayer.Repositories;

public interface IChunkRepository
{
    Task<Chunk?> GetByIdAsync(Guid chunkId);

    Task<IReadOnlyList<Chunk>> GetAllAsync();

    Task<Chunk> CreateAsync(Chunk chunk);

    Task<IReadOnlyList<Chunk>> GetByDocumentIdAsync(Guid documentId);

    Task<bool> DeleteAsync(Guid chunkId);

    IQueryable<Chunk> Query();
}

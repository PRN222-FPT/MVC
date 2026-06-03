using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class ChunkRepository : IChunkRepository
{
    private readonly Prn222Context _context;

    public ChunkRepository(Prn222Context context)
    {
        _context = context;
    }

    public async Task<Chunk?> GetByIdAsync(Guid chunkId)
    {
        return await _context.Chunks
            .AsNoTracking()
            .FirstOrDefaultAsync(chunk => chunk.ChunkId == chunkId);
    }

    public async Task<IReadOnlyList<Chunk>> GetAllAsync()
    {
        return await _context.Chunks
            .AsNoTracking()
            .OrderBy(chunk => chunk.DocumentId)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ToListAsync();
    }

    public async Task<Chunk> CreateAsync(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        await _context.Chunks.AddAsync(chunk);

        return chunk;
    }

    public async Task<IReadOnlyList<Chunk>> GetByDocumentIdAsync(Guid documentId)
    {
        return await _context.Chunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync();
    }

    public async Task<bool> DeleteAsync(Guid chunkId)
    {
        var chunk = await _context.Chunks.FindAsync(chunkId);
        if (chunk is null)
        {
            return false;
        }

        _context.Chunks.Remove(chunk);

        return true;
    }

    public IQueryable<Chunk> Query()
    {
        return _context.Chunks.AsQueryable();
    }
}

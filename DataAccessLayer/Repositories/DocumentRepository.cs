using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly Prn222Context _context;

    public DocumentRepository(Prn222Context context)
    {
        _context = context;
    }

    public async Task<Document?> GetByIdAsync(Guid documentId)
    {
        return await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.DocumentId == documentId);
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync()
    {
        return await _context.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync();
    }

    public async Task<Document> CreateAsync(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _context.Documents.AddAsync(document);
        await _context.SaveChangesAsync();

        return document;
    }

    public async Task<bool> UpdateStatusAsync(Guid documentId, string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Document status is required.", nameof(status));
        }

        var document = await _context.Documents.FindAsync(documentId);
        if (document is null)
        {
            return false;
        }

        document.Status = status.Trim();
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(Guid documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document is null)
        {
            return false;
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IReadOnlyList<Document>> GetByIdsAsync(IEnumerable<Guid> documentIds)
    {
        if (documentIds == null)
        {
            return Array.Empty<Document>();
        }
        return await _context.Documents
            .AsNoTracking()
            .Where(d => documentIds.Contains(d.DocumentId))
            .ToListAsync();
    }

    public IQueryable<Document> Query()
    {
        return _context.Documents.AsQueryable();
    }
}

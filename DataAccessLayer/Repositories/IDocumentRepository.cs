using DataAccessLayer.Models;

namespace DataAccessLayer.Repositories;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid documentId);

    Task<IReadOnlyList<Document>> GetAllAsync();

    Task<Document> CreateAsync(Document document);

    Task<bool> UpdateStatusAsync(Guid documentId, string status);

    Task<bool> DeleteAsync(Guid documentId);

    IQueryable<Document> Query();
}

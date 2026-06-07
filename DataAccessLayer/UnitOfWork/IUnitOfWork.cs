using DataAccessLayer.Repositories;

namespace DataAccessLayer.UnitOfWork;

public interface IUnitOfWork
{
    IDocumentRepository Documents { get; }

    IChunkRepository Chunks { get; }

    IConversationRepository Conversations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

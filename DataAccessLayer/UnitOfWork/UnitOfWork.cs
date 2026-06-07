using DataAccessLayer.Models;
using DataAccessLayer.Repositories;

namespace DataAccessLayer.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly Prn222Context _context;

    public UnitOfWork(
        Prn222Context context,
        IDocumentRepository documents,
        IChunkRepository chunks,
        IConversationRepository conversations)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Documents = documents ?? throw new ArgumentNullException(nameof(documents));
        Chunks = chunks ?? throw new ArgumentNullException(nameof(chunks));
        Conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
    }

    public IDocumentRepository Documents { get; }

    public IChunkRepository Chunks { get; }

    public IConversationRepository Conversations { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}

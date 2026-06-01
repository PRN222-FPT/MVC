using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly Prn222Context _context;

    public ConversationRepository(Prn222Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Session?> GetSessionByIdAsync(Guid sessionId)
    {
        return await _context.Sessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task<Session> CreateSessionAsync(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task AddMessageAsync(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _context.Messages.AddAsync(message);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Message>> GetMessagesBySessionIdAsync(Guid sessionId)
    {
        return await _context.Messages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }
}

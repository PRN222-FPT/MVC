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

    public async Task<Session?> GetSessionByIdForUserAsync(Guid sessionId, Guid userId)
    {
        return await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId);
    }

    public async Task<IReadOnlyList<Session>> GetSessionsByUserIdAsync(Guid userId)
    {
        return await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Messages)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Messages.Max(m => (DateTime?)m.CreatedAt) ?? s.StartedAt)
            .ThenByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<Session> CreateSessionAsync(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _context.Sessions.AddAsync(session);
        return session;
    }

    public async Task AddMessageAsync(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _context.Messages.AddAsync(message);
    }

    public async Task<IReadOnlyList<Message>> GetMessagesBySessionIdAsync(Guid sessionId)
    {
        return await _context.Messages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Message>> GetMessagesBySessionIdForUserAsync(Guid sessionId, Guid userId)
    {
        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Session.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }
}

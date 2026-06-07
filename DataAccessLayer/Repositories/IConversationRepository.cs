using DataAccessLayer.Models;

namespace DataAccessLayer.Repositories;

public interface IConversationRepository
{
    Task<Session?> GetSessionByIdAsync(Guid sessionId);
    Task<Session?> GetSessionByIdForUserAsync(Guid sessionId, Guid userId);
    Task<IReadOnlyList<Session>> GetSessionsByUserIdAsync(Guid userId);
    Task<Session> CreateSessionAsync(Session session);
    Task AddMessageAsync(Message message);
    Task<IReadOnlyList<Message>> GetMessagesBySessionIdAsync(Guid sessionId);
    Task<IReadOnlyList<Message>> GetMessagesBySessionIdForUserAsync(Guid sessionId, Guid userId);
}

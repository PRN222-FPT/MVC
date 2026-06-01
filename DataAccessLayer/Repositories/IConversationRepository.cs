using DataAccessLayer.Models;

namespace DataAccessLayer.Repositories;

public interface IConversationRepository
{
    Task<Session?> GetSessionByIdAsync(Guid sessionId);
    Task<Session> CreateSessionAsync(Session session);
    Task AddMessageAsync(Message message);
    Task<IReadOnlyList<Message>> GetMessagesBySessionIdAsync(Guid sessionId);
}

using System.Threading;
using System.Threading.Tasks;
using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IChatService
{
    Task<ChatQueryResponse> QueryAsync(ChatQueryRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatSessionHistoryDto>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageHistoryDto>> GetSessionMessagesAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

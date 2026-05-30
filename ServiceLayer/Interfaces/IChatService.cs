using System.Threading;
using System.Threading.Tasks;
using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IChatService
{
    Task<ChatQueryResponse> QueryAsync(ChatQueryRequest request, CancellationToken cancellationToken = default);
}

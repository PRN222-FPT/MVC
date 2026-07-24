using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceLayer.DTOs;

namespace MVC.Hubs;

/// <summary>
/// Pushes live document-processing status/progress to clients viewing the Library or
/// document View pages. Clients join a per-document group; the server never targets a
/// specific connection, so no "leave" method is needed — SignalR drops group membership
/// automatically on disconnect.
/// </summary>
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Teacher}")]
public sealed class DocumentProcessingHub : Hub
{
    public const string ClientEvent = "DocumentStatusChanged";

    public Task JoinDocumentGroup(Guid documentId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(documentId));

    public static string GroupName(Guid documentId) => $"document-{documentId}";
}

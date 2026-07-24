using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace MVC.Controllers;

[Authorize(Roles = UserRoles.Student)]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("Chat")]
    [HttpGet("Chat/Index")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("Chat/History")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        Guid? currentUserId = TryGetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new { error = "User is not authenticated." });
        }

        IReadOnlyList<ChatSessionHistoryDto> sessions = await _chatService.GetHistoryAsync(
            currentUserId.Value,
            cancellationToken);

        return Json(sessions.Select(session => new
        {
            sessionId = session.SessionId,
            startedAt = session.StartedAt,
            lastMessageAt = session.LastMessageAt,
            title = session.Title,
            messageCount = session.MessageCount
        }).ToList());
    }

    [HttpGet("Chat/Sessions/{sessionId:guid}/Messages")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Messages(Guid sessionId, CancellationToken cancellationToken)
    {
        Guid? currentUserId = TryGetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new { error = "User is not authenticated." });
        }

        try
        {
            IReadOnlyList<ChatMessageHistoryDto> messages = await _chatService.GetSessionMessagesAsync(
                currentUserId.Value,
                sessionId,
                cancellationToken);

            return Json(messages.Select(message => new
            {
                messageId = message.MessageId,
                sessionId = message.SessionId,
                senderRole = message.SenderRole,
                messageContent = message.MessageContent,
                createdAt = message.CreatedAt
            }).ToList());
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("Chat/Query")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Query([FromBody] ChatQueryApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message content is required." });
        }

        Guid? currentUserId = TryGetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new { error = "User is not authenticated." });
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var chatRequest = new ChatQueryRequest
            {
                SessionId = request.SessionId,
                UserId = currentUserId.Value,
                Message = request.Message.Trim()
            };

            var response = await _chatService.QueryAsync(chatRequest, cancellationToken);
            stopwatch.Stop();

            return Json(new
            {
                sessionId = response.SessionId,
                answer = response.Answer,
                citations = response.Citations.Select(c => new
                {
                    documentId = c.DocumentId,
                    documentTitle = c.DocumentTitle,
                    pageNo = c.PageNo,
                    chunkIndex = c.ChunkIndex,
                    score = c.Score,
                    chunkPreview = c.ChunkPreview,
                    chunkContent = c.ChunkContent
                }).ToList(),
                latency_ms = stopwatch.ElapsedMilliseconds
            });
        }
        catch (UnauthorizedAccessException)
        {
            stopwatch.Stop();
            return Forbid();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error occurred in ChatController.Query for user {UserId}", currentUserId);
            return StatusCode(500, new { error = "An internal error occurred while generating the answer." });
        }
    }

    private Guid? TryGetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out Guid parsed) ? parsed : null;
    }
}

public class ChatQueryApiRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = null!;
}

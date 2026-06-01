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

[Authorize]
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
                answer = response.Answer,
                citations = response.Citations.Select(c => new
                {
                    documentId = c.DocumentId,
                    documentTitle = c.DocumentTitle,
                    pageNo = c.PageNo,
                    chunkIndex = c.ChunkIndex,
                    score = c.Score
                }).ToList(),
                latency_ms = stopwatch.ElapsedMilliseconds
            });
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

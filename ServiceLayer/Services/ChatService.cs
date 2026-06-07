using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Models;
using DataAccessLayer.UnitOfWork;
using Microsoft.Extensions.Logging;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class ChatService : IChatService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRetrievalService _retrievalService;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IUnitOfWork unitOfWork,
        IRetrievalService retrievalService,
        IGeminiService geminiService,
        ILogger<ChatService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _retrievalService = retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));
        _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatQueryResponse> QueryAsync(ChatQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message content cannot be null or empty.", nameof(request));
        }

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting RAG chat pipeline for user {UserId}", request.UserId);

        // 1. Resolve or create Session
        Guid sessionId = request.SessionId ?? Guid.NewGuid();
        var session = sessionId == Guid.Empty
            ? null
            : await _unitOfWork.Conversations.GetSessionByIdForUserAsync(sessionId, request.UserId);
        
        if (session is null)
        {
            if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
            {
                throw new UnauthorizedAccessException("The requested chat session does not belong to the current user.");
            }

            if (sessionId == Guid.Empty)
            {
                sessionId = Guid.NewGuid();
            }

            _logger.LogInformation("Creating new session {SessionId} for user {UserId}", sessionId, request.UserId);
            session = new Session
            {
                SessionId = sessionId,
                UserId = request.UserId,
                StartedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            await _unitOfWork.Conversations.CreateSessionAsync(session);
        }

        // 2. Save User Message to DB
        var userMessage = new Message
        {
            MessageId = Guid.NewGuid(),
            SessionId = sessionId,
            SenderRole = "user",
            MessageContent = request.Message,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        await _unitOfWork.Conversations.AddMessageAsync(userMessage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 3. Retrieve context chunks (RAG Retrieve)
        var contextChunks = await _retrievalService.RetrieveContextAsync(request.Message, cancellationToken);

        // 4. Build prompt context (concatenating retrieved chunks)
        string contextText = string.Empty;
        if (contextChunks.Count > 0)
        {
            contextText = string.Join("\n\n", contextChunks.Select((c, idx) => 
                $"[Source {idx + 1} - Page {c.PageNo}]:\n{c.ChunkText}"));
        }

        // 5. Generate Answer via Gemini
        string answer = await _geminiService.GenerateAnswerAsync(contextText, request.Message, cancellationToken);

        // 6. Save AI Response Message to DB
        var aiMessage = new Message
        {
            MessageId = Guid.NewGuid(),
            SessionId = sessionId,
            SenderRole = "model",
            MessageContent = answer,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        await _unitOfWork.Conversations.AddMessageAsync(aiMessage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Resolve Document Titles for Citations
        var docIds = contextChunks.Select(c => c.DocumentId).Distinct().ToList();
        var docTitles = new Dictionary<Guid, string>();
        if (docIds.Count > 0)
        {
            try
            {
                var docs = await _unitOfWork.Documents.GetByIdsAsync(docIds);
                
                docTitles = docs.ToDictionary(d => d.DocumentId, d => d.Title ?? "Untitled");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve document titles for citations.");
            }
        }

        // Construct citations DTO list
        var citations = contextChunks.Select(c => new CitationDto
        {
            DocumentId = c.DocumentId,
            DocumentTitle = docTitles.TryGetValue(c.DocumentId, out var title) ? title : "Untitled Document",
            PageNo = c.PageNo,
            ChunkIndex = c.ChunkIndex,
            Score = c.Score,
            ChunkPreview = BuildCitationPreview(c.ChunkText),
            ChunkContent = c.ChunkText
        }).ToList();

        stopwatch.Stop();
        long elapsedMs = stopwatch.ElapsedMilliseconds;
        _logger.LogInformation("Completed RAG chat pipeline in {ElapsedMs}ms", elapsedMs);

        return new ChatQueryResponse
        {
            SessionId = sessionId,
            Answer = answer,
            Citations = citations,
            LatencyMs = elapsedMs
        };
    }

    public async Task<IReadOnlyList<ChatSessionHistoryDto>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        IReadOnlyList<Session> sessions = await _unitOfWork.Conversations.GetSessionsByUserIdAsync(userId);

        return sessions
            .Select(session =>
            {
                IReadOnlyList<Message> messages = session.Messages
                    .OrderBy(message => message.CreatedAt)
                    .ToList();

                Message? firstUserMessage = messages.FirstOrDefault(message =>
                    string.Equals(message.SenderRole, "user", StringComparison.OrdinalIgnoreCase));
                DateTime? lastMessageAt = messages
                    .OrderByDescending(message => message.CreatedAt)
                    .FirstOrDefault()
                    ?.CreatedAt;

                return new ChatSessionHistoryDto(
                    session.SessionId,
                    session.StartedAt,
                    lastMessageAt,
                    BuildSessionTitle(firstUserMessage?.MessageContent),
                    messages.Count);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ChatMessageHistoryDto>> GetSessionMessagesAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        Session? session = await _unitOfWork.Conversations.GetSessionByIdForUserAsync(sessionId, userId);
        if (session is null)
        {
            throw new UnauthorizedAccessException("The requested chat session does not belong to the current user.");
        }

        IReadOnlyList<Message> messages = await _unitOfWork.Conversations.GetMessagesBySessionIdForUserAsync(sessionId, userId);

        return messages
            .Select(message => new ChatMessageHistoryDto(
                message.MessageId,
                message.SessionId,
                message.SenderRole,
                message.MessageContent,
                message.CreatedAt))
            .ToList();
    }

    private static string BuildSessionTitle(string? firstUserMessage)
    {
        if (string.IsNullOrWhiteSpace(firstUserMessage))
        {
            return "New chat";
        }

        string title = firstUserMessage.Trim();
        return title.Length > 60 ? $"{title[..60]}..." : title;
    }

    private static string BuildCitationPreview(string chunkText)
    {
        if (string.IsNullOrWhiteSpace(chunkText))
        {
            return string.Empty;
        }

        string normalized = chunkText.Trim();
        return normalized.Length <= 150 ? normalized : $"{normalized[..150]}...";
    }
}

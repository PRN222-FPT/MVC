using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using Microsoft.Extensions.Logging;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class ChatService : IChatService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IRetrievalService _retrievalService;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IConversationRepository conversationRepository,
        IDocumentRepository documentRepository,
        IRetrievalService retrievalService,
        IGeminiService geminiService,
        ILogger<ChatService> logger)
    {
        _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
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
        var session = sessionId == Guid.Empty ? null : await _conversationRepository.GetSessionByIdAsync(sessionId);
        
        if (session is null)
        {
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
            await _conversationRepository.CreateSessionAsync(session);
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
        await _conversationRepository.AddMessageAsync(userMessage);

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
        await _conversationRepository.AddMessageAsync(aiMessage);

        // 7. Resolve Document Titles for Citations
        var docIds = contextChunks.Select(c => c.DocumentId).Distinct().ToList();
        var docTitles = new Dictionary<Guid, string>();
        if (docIds.Count > 0)
        {
            try
            {
                var docs = await _documentRepository.GetByIdsAsync(docIds);
                
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
            Score = c.Score
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
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public class ChatServiceTests
{
    private class FakeConversationRepository : IConversationRepository
    {
        public List<Session> Sessions { get; } = new();
        public List<Message> Messages { get; } = new();

        public Task<Session?> GetSessionByIdAsync(Guid sessionId)
        {
            return Task.FromResult(Sessions.FirstOrDefault(s => s.SessionId == sessionId));
        }

        public Task<Session> CreateSessionAsync(Session session)
        {
            Sessions.Add(session);
            return Task.FromResult(session);
        }

        public Task AddMessageAsync(Message message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Message>> GetMessagesBySessionIdAsync(Guid sessionId)
        {
            IReadOnlyList<Message> list = Messages.Where(m => m.SessionId == sessionId).ToList();
            return Task.FromResult(list);
        }
    }

    private class FakeDocumentRepository : IDocumentRepository
    {
        public List<Document> Documents { get; } = new();

        public Task<Document?> GetByIdAsync(Guid documentId)
        {
            return Task.FromResult(Documents.FirstOrDefault(d => d.DocumentId == documentId));
        }

        public Task<IReadOnlyList<Document>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Document>>(Documents);
        }

        public Task<Document> CreateAsync(Document document)
        {
            Documents.Add(document);
            return Task.FromResult(document);
        }

        public Task<bool> UpdateStatusAsync(Guid documentId, string status)
        {
            var doc = Documents.FirstOrDefault(d => d.DocumentId == documentId);
            if (doc == null) return Task.FromResult(false);
            doc.Status = status;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid documentId)
        {
            var doc = Documents.FirstOrDefault(d => d.DocumentId == documentId);
            if (doc == null) return Task.FromResult(false);
            Documents.Remove(doc);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<Document>> GetByIdsAsync(IEnumerable<Guid> documentIds)
        {
            IReadOnlyList<Document> list = Documents.Where(d => documentIds.Contains(d.DocumentId)).ToList();
            return Task.FromResult(list);
        }

        public IQueryable<Document> Query()
        {
            return Documents.AsQueryable();
        }
    }

    private class FakeRetrievalService : IRetrievalService
    {
        public List<RetrievalResult> ResultsToReturn { get; set; } = new();
        public string? LastQuery { get; private set; }

        public Task<List<RetrievalResult>> RetrieveContextAsync(string query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(ResultsToReturn);
        }
    }

    private class FakeGeminiService : IGeminiService
    {
        public string AnswerToReturn { get; set; } = "Mocked Gemini Answer";
        public string? LastContext { get; private set; }
        public string? LastQuestion { get; private set; }

        public Task<string> GenerateAnswerAsync(string context, string question, CancellationToken cancellationToken = default)
        {
            LastContext = context;
            LastQuestion = question;
            return Task.FromResult(AnswerToReturn);
        }
    }

    [Fact]
    public async Task QueryAsync_NullRequest_ThrowsArgumentNullException()
    {
        var repo = new FakeConversationRepository();
        var docRepo = new FakeDocumentRepository();
        var ret = new FakeRetrievalService();
        var gemini = new FakeGeminiService();
        var chatService = new ChatService(repo, docRepo, ret, gemini, NullLogger<ChatService>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => chatService.QueryAsync(null!));
    }

    [Fact]
    public async Task QueryAsync_EmptyMessage_ThrowsArgumentException()
    {
        var repo = new FakeConversationRepository();
        var docRepo = new FakeDocumentRepository();
        var ret = new FakeRetrievalService();
        var gemini = new FakeGeminiService();
        var chatService = new ChatService(repo, docRepo, ret, gemini, NullLogger<ChatService>.Instance);

        var request = new ChatQueryRequest
        {
            UserId = Guid.NewGuid(),
            Message = "   "
        };

        await Assert.ThrowsAsync<ArgumentException>(() => chatService.QueryAsync(request));
    }

    [Fact]
    public async Task QueryAsync_NewSession_CreatesSessionAndSavesMessages()
    {
        // Arrange
        var repo = new FakeConversationRepository();
        var docRepo = new FakeDocumentRepository();
        var ret = new FakeRetrievalService();
        var gemini = new FakeGeminiService();
        var chatService = new ChatService(repo, docRepo, ret, gemini, NullLogger<ChatService>.Instance);

        var userId = Guid.NewGuid();
        var request = new ChatQueryRequest
        {
            UserId = userId,
            Message = "Hello, tell me about the report."
        };

        // Act
        var response = await chatService.QueryAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, response.SessionId);
        Assert.Equal("Mocked Gemini Answer", response.Answer);
        Assert.Empty(response.Citations);
        Assert.True(response.LatencyMs >= 0);

        // Verify database saves
        Assert.Single(repo.Sessions);
        Assert.Equal(response.SessionId, repo.Sessions[0].SessionId);
        Assert.Equal(userId, repo.Sessions[0].UserId);

        Assert.Equal(2, repo.Messages.Count);
        Assert.Equal("user", repo.Messages[0].SenderRole);
        Assert.Equal("Hello, tell me about the report.", repo.Messages[0].MessageContent);
        Assert.Equal("model", repo.Messages[1].SenderRole);
        Assert.Equal("Mocked Gemini Answer", repo.Messages[1].MessageContent);
    }

    [Fact]
    public async Task QueryAsync_ExistingSession_SavesMessagesInExistingSession()
    {
        // Arrange
        var repo = new FakeConversationRepository();
        var docRepo = new FakeDocumentRepository();
        var ret = new FakeRetrievalService();
        var gemini = new FakeGeminiService();
        var chatService = new ChatService(repo, docRepo, ret, gemini, NullLogger<ChatService>.Instance);

        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        
        var existingSession = new Session { SessionId = sessionId, UserId = userId };
        repo.Sessions.Add(existingSession);

        var request = new ChatQueryRequest
        {
            SessionId = sessionId,
            UserId = userId,
            Message = "Hello"
        };

        // Act
        var response = await chatService.QueryAsync(request);

        // Assert
        Assert.Equal(sessionId, response.SessionId);
        Assert.Single(repo.Sessions); // No new session created

        Assert.Equal(2, repo.Messages.Count);
        Assert.All(repo.Messages, m => Assert.Equal(sessionId, m.SessionId));
    }

    [Fact]
    public async Task QueryAsync_ResolvesCitationsWithDocumentTitles()
    {
        // Arrange
        var repo = new FakeConversationRepository();
        var docRepo = new FakeDocumentRepository();
        var ret = new FakeRetrievalService();
        var gemini = new FakeGeminiService();
        var chatService = new ChatService(repo, docRepo, ret, gemini, NullLogger<ChatService>.Instance);

        var docId = Guid.NewGuid();
        var doc = new Document { DocumentId = docId, Title = "Q3 Earnings Report" };
        docRepo.Documents.Add(doc);

        ret.ResultsToReturn.Add(new RetrievalResult("Relevant financial info...", 0.85f, 3, 12, docId));

        var request = new ChatQueryRequest
        {
            UserId = Guid.NewGuid(),
            Message = "What was the revenue?"
        };

        // Act
        var response = await chatService.QueryAsync(request);

        // Assert
        Assert.Single(response.Citations);
        Assert.Equal(docId, response.Citations[0].DocumentId);
        Assert.Equal("Q3 Earnings Report", response.Citations[0].DocumentTitle);
        Assert.Equal(3, response.Citations[0].PageNo);
        Assert.Equal(12, response.Citations[0].ChunkIndex);
        Assert.Equal(0.85f, response.Citations[0].Score);

        // Check if correct context format was generated
        Assert.Contains("[Source 1 - Page 3]:\nRelevant financial info...", gemini.LastContext);
    }
}

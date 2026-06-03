using System;
using System.Collections.Generic;

namespace ServiceLayer.DTOs;

public sealed class ChatQueryRequest
{
    public Guid? SessionId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = null!;
}

public sealed class ChatQueryResponse
{
    public Guid SessionId { get; set; }
    public string Answer { get; set; } = null!;
    public List<CitationDto> Citations { get; set; } = new();
    public long LatencyMs { get; set; }
}

public sealed record ChatSessionHistoryDto(
    Guid SessionId,
    DateTime? StartedAt,
    DateTime? LastMessageAt,
    string Title,
    int MessageCount);

public sealed record ChatMessageHistoryDto(
    Guid MessageId,
    Guid SessionId,
    string SenderRole,
    string MessageContent,
    DateTime? CreatedAt);

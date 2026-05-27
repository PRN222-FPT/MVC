using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Background processing for a queued document.
///
/// SCOPE (Task 3): this is a SKELETON. It advances the ProcessingJob/Document status
/// (queued -> processing -> done) so the pipeline is observable end-to-end, but it does
/// NOT parse text or create chunks — that belongs to a separate task (parsing/chunking).
/// The real implementation will call the PDF/DOCX parser + embedding/chunking here.
/// </summary>
public sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly Prn222Context _context;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        Prn222Context context,
        IDocumentRepository documentRepository,
        ILogger<DocumentProcessor> logger)
    {
        _context = context;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        ProcessingJob? job = await _context.ProcessingJobs
            .Where(j => j.DocumentId == documentId)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        try
        {
            if (job is not null)
            {
                job.JobStatus = "processing";
                job.StartedAt = UnspecifiedNow();
                await _context.SaveChangesAsync(cancellationToken);
            }

            // ───────────────────────────────────────────────────────────────
            // TODO (separate task): parse PDF/DOCX via the DocumentParser,
            // chunk the text, generate embeddings, and persist Chunk rows here.
            // For now we only simulate completion so the status flow is testable.
            // ───────────────────────────────────────────────────────────────
            _logger.LogInformation(
                "Processing document {DocumentId} (skeleton — parsing/chunking not implemented in this task)",
                documentId);

            await _documentRepository.UpdateStatusAsync(documentId, "processed");

            if (job is not null)
            {
                job.JobStatus = "done";
                job.FinishedAt = UnspecifiedNow();
                await _context.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Document {DocumentId} marked processed", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for document {DocumentId}", documentId);

            if (job is not null)
            {
                job.JobStatus = "failed";
                job.FinishedAt = UnspecifiedNow();
                job.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync(CancellationToken.None);
            }

            await _documentRepository.UpdateStatusAsync(documentId, "failed");
        }
    }

    // The processing_jobs timestamp columns are "timestamp without time zone".
    // Npgsql rejects a UTC-kind DateTime for those, so store an Unspecified-kind value.
    private static DateTime UnspecifiedNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}

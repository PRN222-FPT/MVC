using DataAccessLayer.Models;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class SubjectService : ISubjectService
{
    private readonly Prn222Context _context;
    private readonly IUnitOfWork _unitOfWork;

    public SubjectService(Prn222Context context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SubjectListItemDto>> GetSubjectsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Subjects
            .AsNoTracking()
            .OrderBy(subject => subject.SubjectCode)
            .ThenBy(subject => subject.SubjectName)
            .Select(subject => new SubjectListItemDto(
                subject.SubjectId,
                subject.SubjectCode,
                subject.SubjectName,
                subject.Description,
                subject.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateSubjectResultDto> CreateSubjectAsync(
        CreateSubjectDto request,
        CancellationToken cancellationToken = default)
    {
        string subjectCode = NormalizeSubjectCode(request.SubjectCode);
        string subjectName = request.SubjectName.Trim();

        if (string.IsNullOrWhiteSpace(subjectCode))
        {
            return new CreateSubjectResultDto(false, "Subject code is required.");
        }

        if (string.IsNullOrWhiteSpace(subjectName))
        {
            return new CreateSubjectResultDto(false, "Subject name is required.");
        }

        bool exists = await _context.Subjects
            .AnyAsync(subject => subject.SubjectCode.ToLower() == subjectCode.ToLower(), cancellationToken);
        if (exists)
        {
            return new CreateSubjectResultDto(false, "A subject with this code already exists.");
        }

        _context.Subjects.Add(new Subject
        {
            SubjectId = Guid.NewGuid(),
            SubjectCode = subjectCode,
            SubjectName = subjectName.Length > 255 ? subjectName[..255] : subjectName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateSubjectResultDto(true, null);
    }

    private static string NormalizeSubjectCode(string subjectCode)
    {
        string normalized = subjectCode.Trim().ToUpperInvariant();
        return normalized.Length > 50 ? normalized[..50] : normalized;
    }
}

using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface ISubjectService
{
    Task<IReadOnlyList<SubjectListItemDto>> GetSubjectsAsync(CancellationToken cancellationToken = default);

    Task<CreateSubjectResultDto> CreateSubjectAsync(
        CreateSubjectDto request,
        CancellationToken cancellationToken = default);
}

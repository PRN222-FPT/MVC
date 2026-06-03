namespace ServiceLayer.DTOs;

public sealed record SubjectListItemDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    string? Description,
    DateTime? CreatedAt);

public sealed record CreateSubjectDto(
    string SubjectCode,
    string SubjectName,
    string? Description);

public sealed record CreateSubjectResultDto(
    bool Succeeded,
    string? ErrorMessage);

public sealed record TeacherUploadSubjectDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName);

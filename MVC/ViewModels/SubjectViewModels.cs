using System.ComponentModel.DataAnnotations;

namespace MVC.ViewModels;

public sealed class SubjectsIndexViewModel
{
    public CreateSubjectViewModel CreateSubject { get; set; } = new();

    public IReadOnlyList<SubjectListItemViewModel> Subjects { get; set; } = [];
}

public sealed class SubjectListItemViewModel
{
    public Guid SubjectId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;

    public string SubjectName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }
}

public sealed class CreateSubjectViewModel
{
    [Required(ErrorMessage = "Subject code is required.")]
    [StringLength(50, ErrorMessage = "Subject code cannot exceed 50 characters.")]
    public string SubjectCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Subject name is required.")]
    [StringLength(255, ErrorMessage = "Subject name cannot exceed 255 characters.")]
    public string SubjectName { get; set; } = string.Empty;

    public string? Description { get; set; }
}

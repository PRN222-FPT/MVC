using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace MVC.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public sealed class SubjectsController : Controller
{
    private readonly ISubjectService _subjectService;

    public SubjectsController(ISubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildIndexViewModelAsync(new CreateSubjectViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "CreateSubject")] CreateSubjectViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(viewModel, cancellationToken));
        }

        CreateSubjectResultDto result = await _subjectService.CreateSubjectAsync(
            new CreateSubjectDto(
                viewModel.SubjectCode,
                viewModel.SubjectName,
                viewModel.Description),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create the subject.");
            return View("Index", await BuildIndexViewModelAsync(viewModel, cancellationToken));
        }

        TempData["Success"] = "Subject created.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<SubjectsIndexViewModel> BuildIndexViewModelAsync(
        CreateSubjectViewModel createSubject,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectListItemDto> subjects = await _subjectService.GetSubjectsAsync(cancellationToken);

        return new SubjectsIndexViewModel
        {
            CreateSubject = createSubject,
            Subjects = subjects.Select(subject => new SubjectListItemViewModel
            {
                SubjectId = subject.SubjectId,
                SubjectCode = subject.SubjectCode,
                SubjectName = subject.SubjectName,
                Description = subject.Description,
                CreatedAt = subject.CreatedAt
            }).ToList()
        };
    }
}

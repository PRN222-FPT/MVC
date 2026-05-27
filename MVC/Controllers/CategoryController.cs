using Microsoft.AspNetCore.Mvc;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace MVC.Controllers;

public class CategoryController : Controller
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    // GET: Category
    public async Task<IActionResult> Index()
    {
        var categories = await _categoryService.GetAllAsync();
        var viewModels = categories.Select(c => new CategoryIndexViewModel
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            Description = c.Description,
            ProductCount = c.ProductCount
        });

        return View(viewModels);
    }

    // GET: Category/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null) return NotFound();

        var viewModel = new CategoryDetailsViewModel
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Description = category.Description,
            ProductCount = category.ProductCount
        };

        return View(viewModel);
    }

    // GET: Category/Create
    public IActionResult Create() => View();

    // POST: Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryCreateViewModel viewModel)
    {
        if (!ModelState.IsValid) return View(viewModel);

        var dto = new CategoryCreateDto(viewModel.Name, viewModel.Description);
        await _categoryService.CreateAsync(dto);

        return RedirectToAction(nameof(Index));
    }

    // GET: Category/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null) return NotFound();

        var viewModel = new CategoryEditViewModel
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Description = category.Description
        };

        return View(viewModel);
    }

    // POST: Category/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryEditViewModel viewModel)
    {
        if (id != viewModel.CategoryId) return BadRequest();
        if (!ModelState.IsValid) return View(viewModel);

        var dto = new CategoryUpdateDto(viewModel.Name, viewModel.Description);
        var success = await _categoryService.UpdateAsync(id, dto);

        if (!success) return NotFound();

        return RedirectToAction(nameof(Index));
    }

    // GET: Category/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null) return NotFound();

        var viewModel = new CategoryDeleteViewModel
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Description = category.Description,
            ProductCount = category.ProductCount
        };

        return View(viewModel);
    }

    // POST: Category/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var success = await _categoryService.DeleteAsync(id);

        if (!success)
        {
            TempData["Error"] = "Cannot delete category that still has products.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        return RedirectToAction(nameof(Index));
    }
}

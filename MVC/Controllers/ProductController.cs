using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace MVC.Controllers;

[Authorize(Roles = UserRoles.DisabledLegacyRoute)]
public class ProductController : Controller
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public ProductController(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    // GET: Product
    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();
        var viewModels = products.Select(p => new ProductIndexViewModel
        {
            ProductId = p.ProductId,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            CategoryName = p.CategoryName
        });

        return View(viewModels);
    }

    // GET: Product/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null) return NotFound();

        var viewModel = new ProductDetailsViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName
        };

        return View(viewModel);
    }

    // GET: Product/Create
    public async Task<IActionResult> Create()
    {
        var viewModel = new ProductCreateViewModel
        {
            Categories = await GetCategorySelectListAsync()
        };

        return View(viewModel);
    }

    // POST: Product/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateViewModel viewModel)
    {
        if (!ModelState.IsValid)
        {
            viewModel.Categories = await GetCategorySelectListAsync();
            return View(viewModel);
        }

        var dto = new ProductCreateDto(
            viewModel.Name,
            viewModel.Description,
            viewModel.Price,
            viewModel.StockQuantity,
            viewModel.CategoryId);

        await _productService.CreateAsync(dto);

        return RedirectToAction(nameof(Index));
    }

    // GET: Product/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null) return NotFound();

        var viewModel = new ProductEditViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId,
            Categories = await GetCategorySelectListAsync()
        };

        return View(viewModel);
    }

    // POST: Product/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductEditViewModel viewModel)
    {
        if (id != viewModel.ProductId) return BadRequest();

        if (!ModelState.IsValid)
        {
            viewModel.Categories = await GetCategorySelectListAsync();
            return View(viewModel);
        }

        var dto = new ProductUpdateDto(
            viewModel.Name,
            viewModel.Description,
            viewModel.Price,
            viewModel.StockQuantity,
            viewModel.CategoryId);

        var success = await _productService.UpdateAsync(id, dto);
        if (!success) return NotFound();

        return RedirectToAction(nameof(Index));
    }

    // GET: Product/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null) return NotFound();

        var viewModel = new ProductDeleteViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryName = product.CategoryName
        };

        return View(viewModel);
    }

    // POST: Product/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var success = await _productService.DeleteAsync(id);
        if (!success) return NotFound();

        return RedirectToAction(nameof(Index));
    }

    private async Task<SelectList> GetCategorySelectListAsync()
    {
        var categories = await _categoryService.GetAllAsync();
        return new SelectList(
            categories.Select(c => new { c.CategoryId, c.Name }),
            "CategoryId",
            "Name");
    }
}

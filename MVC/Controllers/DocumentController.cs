using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers;

public class DocumentController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Library()
    {
        return View();
    }

    public IActionResult Upload()
    {
        return View();
    }
}

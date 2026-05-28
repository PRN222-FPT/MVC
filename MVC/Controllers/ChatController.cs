using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers;

public class ChatController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

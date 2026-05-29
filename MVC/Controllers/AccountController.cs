using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers;

public class AccountController : Controller
{
    public IActionResult Login()
    {
        return View();
    }

    public IActionResult Logout()
    {
        return RedirectToAction("Login");
    }
}

using Microsoft.AspNetCore.Mvc;
using MVC.Models;
using System.Diagnostics;

namespace MVC.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode)
    {
        var code = statusCode ?? 500;

        Response.StatusCode = code;

        var model = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code,
            Message = code switch
            {
                400 => "The request was invalid.",
                403 => "You do not have permission to access this resource.",
                404 => "The requested resource was not found.",
                409 => "A conflict occurred while processing your request.",
                _ => "An unexpected error occurred. Please try again later."
            }
        };

        return View(model);
    }
}

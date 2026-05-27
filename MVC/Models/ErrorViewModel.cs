namespace MVC.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public int StatusCode { get; set; } = 500;

    public string Message { get; set; } = "An unexpected error occurred.";

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

namespace ServiceLayer.Interfaces;

public interface IEmailSender
{
    Task SendStudentWelcomeEmailAsync(
        string email,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(
        string email,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
}

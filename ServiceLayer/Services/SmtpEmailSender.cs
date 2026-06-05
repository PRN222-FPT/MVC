using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendStudentWelcomeEmailAsync(
        string email,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        await SendStudentAccountEmailAsync(
            email,
            fullName,
            temporaryPassword,
            "Your student account has been created",
            "Your student account has been created.",
            cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(
        string email,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        await SendStudentAccountEmailAsync(
            email,
            fullName,
            temporaryPassword,
            "Your account password has been reset",
            "Your account password has been reset by an administrator.",
            cancellationToken);
    }

    private async Task SendStudentAccountEmailAsync(
        string email,
        string fullName,
        string temporaryPassword,
        string subject,
        string intro,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("SMTP email settings are not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = $"""
Hello {fullName},

{intro}

Email: {email}
Temporary password: {temporaryPassword}

Please sign in and change your password after first login if your class policy requires it.
""",
            IsBodyHtml = false
        };
        message.To.Add(email);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException ex)
        {
            _logger.LogWarning(
                ex,
                "SMTP send failed. Host: {SmtpHost}; Port: {SmtpPort}; EnableSsl: {EnableSsl}; FromAddressConfigured: {FromAddressConfigured}; UsernameConfigured: {UsernameConfigured}; StatusCode: {StatusCode}",
                _options.Host,
                _options.Port,
                _options.EnableSsl,
                !string.IsNullOrWhiteSpace(_options.FromAddress),
                !string.IsNullOrWhiteSpace(_options.Username),
                ex.StatusCode);
            throw;
        }
    }
}

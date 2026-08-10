using System.Net;
using System.Net.Mail;
using LanguagePractice.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanguagePractice.Infrastructure.Services;

public class EmailOptions
{
    public const string SectionName = "Email";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string FromAddress { get; set; } = "noreply@linguatalk.app";
    public string FromName { get; set; } = "LinguaTalk";
    public bool UseSsl { get; set; } = true;
}

/// <summary>
/// SMTP yapılandırılmışsa gerçek mail; değilse log'a yazar (dev).
/// </summary>
public class AppEmailSender : IAppEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<AppEmailSender> _logger;

    public AppEmailSender(IOptions<EmailOptions> options, ILogger<AppEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogInformation(
                "Email (dev/log) To={Email} Subject={Subject}\n{Body}",
                email, subject, htmlMessage);
            return;
        }

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.SmtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.SmtpUser, _options.SmtpPassword)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        message.To.Add(email);
        await client.SendMailAsync(message);
    }
}

namespace LanguagePractice.Core.Interfaces;

public interface IAppEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);
}

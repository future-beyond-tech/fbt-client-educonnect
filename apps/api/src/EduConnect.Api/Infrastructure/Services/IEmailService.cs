namespace EduConnect.Api.Infrastructure.Services;

public interface IEmailService
{
    /// <summary>
    /// Sends a transactional email. Implementations are expected to log
    /// failures rather than throw, so that callers can decide whether to
    /// expose the error to the end user or fail silently (e.g. to avoid
    /// account enumeration on forgot-password endpoints).
    /// </summary>
    Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default);
}

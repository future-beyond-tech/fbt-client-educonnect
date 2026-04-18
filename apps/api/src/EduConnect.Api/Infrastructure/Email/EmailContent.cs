namespace EduConnect.Api.Infrastructure.Email;

/// <summary>
/// The rendered output of an email template: the subject line,
/// the full HTML body (including the shared layout shell), and
/// a plain-text alternate for clients that prefer it.
/// </summary>
public sealed record EmailContent(string Subject, string Html, string Text);

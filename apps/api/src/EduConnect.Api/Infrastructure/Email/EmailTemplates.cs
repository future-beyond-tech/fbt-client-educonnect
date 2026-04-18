using System.Globalization;
using System.Net;
using System.Text;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Infrastructure.Email;

/// <summary>
/// Builders for the transactional email templates EduConnect sends:
/// welcome-staff, welcome-parent, notice, and student-absent.
/// All templates share the card-style layout defined by <see cref="EmailLayout"/>.
/// </summary>
public static class EmailTemplates
{
    /// <summary>
    /// Welcome email for a newly created Teacher or Admin account. Surfaces
    /// the temporary password once and points the user at the login page,
    /// where they'll be forced to set a new password on first sign-in.
    /// </summary>
    public static EmailContent BuildWelcomeStaff(
        SchoolEntity school,
        string userName,
        string role,
        string loginUrl,
        string tempPassword,
        string logoUrl)
    {
        ArgumentNullException.ThrowIfNull(school);

        var safeName = WebUtility.HtmlEncode(userName);
        var safeRole = WebUtility.HtmlEncode(role);
        var safeSchool = WebUtility.HtmlEncode(school.Name);

        var inner = new StringBuilder();
        inner.Append(EmailLayout.Paragraph($"Hi <strong>{safeName}</strong>,"));
        inner.Append(EmailLayout.Paragraph(
            $"Welcome to <strong>{safeSchool}</strong> on EduConnect. An account has been created for you as a <strong>{safeRole}</strong>."));
        inner.Append(EmailLayout.Paragraph("Use the temporary password below to sign in. You'll be asked to set your own password right after your first login."));
        inner.Append(EmailLayout.CredentialBox("Temporary Password", tempPassword));
        inner.Append(EmailLayout.Button(loginUrl, "Sign in to EduConnect"));
        inner.Append(EmailLayout.Paragraph(
            $"If the button doesn't work, open this link in your browser:<br/><span style=\"color:#6B7280;word-break:break-all;\">{WebUtility.HtmlEncode(loginUrl)}</span>"));
        inner.Append(EmailLayout.Paragraph("If you weren't expecting this invitation, please contact your school administrator."));

        var html = EmailLayout.RenderLayout(
            school,
            logoUrl,
            preheader: $"Your {school.Name} EduConnect account is ready — here's how to sign in.",
            innerHtml: inner.ToString());

        var textBody = new StringBuilder()
            .AppendLine($"Hi {userName},")
            .AppendLine()
            .AppendLine($"Welcome to {school.Name} on EduConnect. An account has been created for you as a {role}.")
            .AppendLine()
            .AppendLine("Use the temporary password below to sign in. You'll be asked to set your own password right after your first login.")
            .AppendLine()
            .AppendLine($"Temporary password: {tempPassword}")
            .AppendLine()
            .AppendLine($"Sign in: {loginUrl}")
            .AppendLine()
            .AppendLine("If you weren't expecting this invitation, please contact your school administrator.")
            .Append(EmailLayout.TextFooter(school))
            .ToString();

        return new EmailContent(
            Subject: $"Welcome to {school.Name} on EduConnect",
            Html: html,
            Text: textBody);
    }

    /// <summary>
    /// Welcome email for a newly created Parent account. Surfaces the
    /// temporary PIN once and points the parent at the login page, where
    /// they'll be forced to change their PIN on first sign-in.
    /// </summary>
    public static EmailContent BuildWelcomeParent(
        SchoolEntity school,
        string parentName,
        string studentName,
        string loginUrl,
        string tempPin,
        string logoUrl)
    {
        ArgumentNullException.ThrowIfNull(school);

        var safeName = WebUtility.HtmlEncode(parentName);
        var safeStudent = WebUtility.HtmlEncode(studentName);
        var safeSchool = WebUtility.HtmlEncode(school.Name);

        var inner = new StringBuilder();
        inner.Append(EmailLayout.Paragraph($"Hi <strong>{safeName}</strong>,"));
        inner.Append(EmailLayout.Paragraph(
            $"Your parent account for <strong>{safeStudent}</strong> at <strong>{safeSchool}</strong> has been created on EduConnect. You can now follow your child's attendance, homework, notices, and more."));
        inner.Append(EmailLayout.Paragraph("Use the temporary PIN below to sign in for the first time. You'll be asked to choose your own PIN right after your first login."));
        inner.Append(EmailLayout.CredentialBox("Temporary PIN", tempPin));
        inner.Append(EmailLayout.Button(loginUrl, "Open EduConnect"));
        inner.Append(EmailLayout.Paragraph(
            $"If the button doesn't work, open this link in your browser:<br/><span style=\"color:#6B7280;word-break:break-all;\">{WebUtility.HtmlEncode(loginUrl)}</span>"));
        inner.Append(EmailLayout.Paragraph("If you weren't expecting this email, please contact the school office."));

        var html = EmailLayout.RenderLayout(
            school,
            logoUrl,
            preheader: $"Your EduConnect parent account for {studentName} is ready.",
            innerHtml: inner.ToString());

        var textBody = new StringBuilder()
            .AppendLine($"Hi {parentName},")
            .AppendLine()
            .AppendLine($"Your parent account for {studentName} at {school.Name} has been created on EduConnect.")
            .AppendLine()
            .AppendLine("Use the temporary PIN below to sign in for the first time. You'll be asked to choose your own PIN right after your first login.")
            .AppendLine()
            .AppendLine($"Temporary PIN: {tempPin}")
            .AppendLine()
            .AppendLine($"Sign in: {loginUrl}")
            .AppendLine()
            .AppendLine("If you weren't expecting this email, please contact the school office.")
            .Append(EmailLayout.TextFooter(school))
            .ToString();

        return new EmailContent(
            Subject: $"Welcome to {school.Name} on EduConnect",
            Html: html,
            Text: textBody);
    }

    /// <summary>
    /// Notice published by the school. Shows the notice title, published
    /// date, and body excerpt, with a button linking into the app.
    /// </summary>
    public static EmailContent BuildNotice(
        SchoolEntity school,
        string recipientName,
        string noticeTitle,
        string noticeBody,
        DateTimeOffset publishedAt,
        string viewUrl,
        string logoUrl)
    {
        ArgumentNullException.ThrowIfNull(school);

        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeTitle = WebUtility.HtmlEncode(noticeTitle);
        var safeSchool = WebUtility.HtmlEncode(school.Name);

        // Preserve paragraphs / line breaks in the body but still encode HTML.
        var safeBody = FormatBodyHtml(noticeBody);

        var publishedText = publishedAt
            .ToLocalTime()
            .ToString("MMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);

        var inner = new StringBuilder();
        inner.Append(EmailLayout.Paragraph($"Hi <strong>{safeRecipient}</strong>,"));
        inner.Append(EmailLayout.Paragraph(
            $"<strong>{safeSchool}</strong> has published a new notice."));
        inner.Append($"""
                     <div style="margin:20px 0;padding:20px;border-left:4px solid #4F46E5;background-color:#F5F3FF;border-radius:8px;">
                         <div style="font-size:17px;font-weight:600;color:#1F2937;margin-bottom:6px;">{safeTitle}</div>
                         <div style="font-size:12px;color:#6B7280;margin-bottom:12px;">Published {WebUtility.HtmlEncode(publishedText)}</div>
                         <div style="font-size:15px;color:#1F2937;line-height:1.6;">{safeBody}</div>
                     </div>
                     """);
        inner.Append(EmailLayout.Button(viewUrl, "View notice in EduConnect"));
        inner.Append(EmailLayout.Paragraph("You're receiving this because you're registered at the school on EduConnect."));

        var html = EmailLayout.RenderLayout(
            school,
            logoUrl,
            preheader: $"New notice from {school.Name}: {noticeTitle}",
            innerHtml: inner.ToString());

        var textBody = new StringBuilder()
            .AppendLine($"Hi {recipientName},")
            .AppendLine()
            .AppendLine($"{school.Name} has published a new notice.")
            .AppendLine()
            .AppendLine($"Title: {noticeTitle}")
            .AppendLine($"Published: {publishedText}")
            .AppendLine()
            .AppendLine(noticeBody)
            .AppendLine()
            .AppendLine($"View in EduConnect: {viewUrl}")
            .Append(EmailLayout.TextFooter(school))
            .ToString();

        return new EmailContent(
            Subject: $"[{school.Name}] Notice: {noticeTitle}",
            Html: html,
            Text: textBody);
    }

    /// <summary>
    /// Absence alert sent to the parents of a student marked absent.
    /// </summary>
    public static EmailContent BuildAbsence(
        SchoolEntity school,
        string parentName,
        string studentName,
        string className,
        DateOnly absenceDate,
        string? reason,
        string viewUrl,
        string logoUrl)
    {
        ArgumentNullException.ThrowIfNull(school);

        var safeParent = WebUtility.HtmlEncode(parentName);
        var safeStudent = WebUtility.HtmlEncode(studentName);

        var formattedDate = absenceDate.ToString("dddd, MMM d, yyyy", CultureInfo.InvariantCulture);

        var inner = new StringBuilder();
        inner.Append(EmailLayout.Paragraph($"Hi <strong>{safeParent}</strong>,"));
        inner.Append(EmailLayout.Paragraph(
            $"This is to let you know that <strong>{safeStudent}</strong> was marked <strong style=\"color:#B91C1C;\">absent</strong> today."));

        var details = new List<(string Label, string Value)>
        {
            ("Student", studentName),
            ("Class", className),
            ("Date", formattedDate),
        };
        if (!string.IsNullOrWhiteSpace(reason))
        {
            details.Add(("Reason", reason));
        }
        inner.Append(EmailLayout.DetailBlock(details));

        inner.Append(EmailLayout.Button(viewUrl, "View attendance in EduConnect"));
        inner.Append(EmailLayout.Paragraph(
            "If you believe this was recorded in error, or you'd like to submit a leave request, please reply through EduConnect or contact the school office."));

        var html = EmailLayout.RenderLayout(
            school,
            logoUrl,
            preheader: $"{studentName} was marked absent on {formattedDate}.",
            innerHtml: inner.ToString());

        var textBody = new StringBuilder()
            .AppendLine($"Hi {parentName},")
            .AppendLine()
            .AppendLine($"This is to let you know that {studentName} was marked absent today.")
            .AppendLine()
            .AppendLine($"Student: {studentName}")
            .AppendLine($"Class:   {className}")
            .AppendLine($"Date:    {formattedDate}");
        if (!string.IsNullOrWhiteSpace(reason))
        {
            textBody.AppendLine($"Reason:  {reason}");
        }
        textBody.AppendLine()
            .AppendLine($"View attendance: {viewUrl}")
            .AppendLine()
            .AppendLine("If you believe this was recorded in error, or you'd like to submit a leave request, please reply through EduConnect or contact the school office.")
            .Append(EmailLayout.TextFooter(school));

        return new EmailContent(
            Subject: $"[{school.Name}] {studentName} was marked absent on {formattedDate}",
            Html: html,
            Text: textBody.ToString());
    }

    /// <summary>
    /// HTML-encode the body and preserve paragraph breaks. Double newlines
    /// become paragraph breaks, single newlines become &lt;br/&gt;.
    /// </summary>
    private static string FormatBodyHtml(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Replace("\r\n", "\n").Replace("\r", "\n");
        var paragraphs = normalized.Split("\n\n", StringSplitOptions.None);
        var sb = new StringBuilder();
        var first = true;
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                continue;
            }

            var encoded = WebUtility.HtmlEncode(paragraph).Replace("\n", "<br/>");
            if (!first)
            {
                sb.Append("<br/><br/>");
            }
            sb.Append(encoded);
            first = false;
        }

        return sb.ToString();
    }
}

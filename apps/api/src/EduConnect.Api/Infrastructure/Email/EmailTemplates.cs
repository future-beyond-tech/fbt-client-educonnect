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
    /// Exam schedule published email. Shows the exam name, class, academic
    /// year, and a subject-by-subject timetable. Sent to parents of students
    /// in the target class when a class teacher publishes the schedule.
    /// </summary>
    public static EmailContent BuildExamSchedule(
        SchoolEntity school,
        string recipientName,
        string? studentName,
        string examName,
        string className,
        string section,
        string academicYear,
        DateTimeOffset publishedAt,
        IEnumerable<ExamScheduleSubjectLine> subjects,
        string viewUrl,
        string logoUrl)
    {
        ArgumentNullException.ThrowIfNull(school);

        var safeRecipient = WebUtility.HtmlEncode(recipientName);
        var safeExam = WebUtility.HtmlEncode(examName);
        var safeClass = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(section) ? className : $"{className} - {section}");
        var safeYear = WebUtility.HtmlEncode(academicYear);
        var safeSchool = WebUtility.HtmlEncode(school.Name);
        var safeStudent = string.IsNullOrWhiteSpace(studentName) ? null : WebUtility.HtmlEncode(studentName);

        var publishedText = publishedAt
            .ToLocalTime()
            .ToString("MMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);

        var orderedSubjects = subjects
            .OrderBy(s => s.ExamDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        var tableRows = new StringBuilder();
        tableRows.Append("""
                         <tr style="background-color:#F5F3FF;">
                             <th align="left" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#4338CA;border-bottom:1px solid #E5E7EB;">Subject</th>
                             <th align="left" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#4338CA;border-bottom:1px solid #E5E7EB;">Date</th>
                             <th align="left" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#4338CA;border-bottom:1px solid #E5E7EB;">Time</th>
                             <th align="left" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#4338CA;border-bottom:1px solid #E5E7EB;">Room</th>
                             <th align="right" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#4338CA;border-bottom:1px solid #E5E7EB;">Max</th>
                         </tr>
                         """);

        foreach (var line in orderedSubjects)
        {
            var dateStr = line.ExamDate.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
            var timeStr = $"{line.StartTime.ToString("h:mm tt", CultureInfo.InvariantCulture)} - {line.EndTime.ToString("h:mm tt", CultureInfo.InvariantCulture)}";
            var room = string.IsNullOrWhiteSpace(line.Room) ? "—" : line.Room;

            tableRows.Append($"""
                              <tr>
                                  <td style="padding:10px 12px;font-size:14px;color:#1F2937;border-bottom:1px solid #E5E7EB;">{WebUtility.HtmlEncode(line.Subject)}</td>
                                  <td style="padding:10px 12px;font-size:14px;color:#1F2937;border-bottom:1px solid #E5E7EB;">{WebUtility.HtmlEncode(dateStr)}</td>
                                  <td style="padding:10px 12px;font-size:14px;color:#1F2937;border-bottom:1px solid #E5E7EB;">{WebUtility.HtmlEncode(timeStr)}</td>
                                  <td style="padding:10px 12px;font-size:14px;color:#6B7280;border-bottom:1px solid #E5E7EB;">{WebUtility.HtmlEncode(room)}</td>
                                  <td align="right" style="padding:10px 12px;font-size:14px;color:#1F2937;border-bottom:1px solid #E5E7EB;">{line.MaxMarks.ToString("0.##", CultureInfo.InvariantCulture)}</td>
                              </tr>
                              """);
        }

        var inner = new StringBuilder();
        inner.Append(EmailLayout.Paragraph($"Hi <strong>{safeRecipient}</strong>,"));

        if (safeStudent is not null)
        {
            inner.Append(EmailLayout.Paragraph(
                $"<strong>{safeSchool}</strong> has published the <strong>{safeExam}</strong> timetable for <strong>{safeStudent}</strong>'s class."));
        }
        else
        {
            inner.Append(EmailLayout.Paragraph(
                $"<strong>{safeSchool}</strong> has published the <strong>{safeExam}</strong> timetable for <strong>{safeClass}</strong>."));
        }

        inner.Append(EmailLayout.DetailBlock(new List<(string, string)>
        {
            ("Exam", examName),
            ("Class", string.IsNullOrWhiteSpace(section) ? className : $"{className} - {section}"),
            ("Academic Year", academicYear),
            ("Published", publishedText),
        }));

        inner.Append($"""
                     <div style="margin:20px 0;overflow:auto;">
                         <table width="100%" cellpadding="0" cellspacing="0" role="presentation" style="border-collapse:collapse;border:1px solid #E5E7EB;border-radius:12px;overflow:hidden;">
                             {tableRows}
                         </table>
                     </div>
                     """);

        inner.Append(EmailLayout.Button(viewUrl, "View full schedule in EduConnect"));
        inner.Append(EmailLayout.Paragraph(
            "Please note exam dates and times carefully. If you believe any detail is incorrect, contact the class teacher or the school office."));

        var html = EmailLayout.RenderLayout(
            school,
            logoUrl,
            preheader: $"Exam schedule for {examName} - {(string.IsNullOrWhiteSpace(section) ? className : $"{className} {section}")}",
            innerHtml: inner.ToString());

        var textBody = new StringBuilder()
            .AppendLine($"Hi {recipientName},")
            .AppendLine()
            .AppendLine(safeStudent is not null
                ? $"{school.Name} has published the {examName} timetable for {studentName}'s class."
                : $"{school.Name} has published the {examName} timetable for {(string.IsNullOrWhiteSpace(section) ? className : $"{className} - {section}")}.")
            .AppendLine()
            .AppendLine($"Exam:          {examName}")
            .AppendLine($"Class:         {(string.IsNullOrWhiteSpace(section) ? className : $"{className} - {section}")}")
            .AppendLine($"Academic Year: {academicYear}")
            .AppendLine($"Published:     {publishedText}")
            .AppendLine()
            .AppendLine("Timetable:");

        foreach (var line in orderedSubjects)
        {
            textBody.AppendLine(
                $"  - {line.Subject}: " +
                $"{line.ExamDate.ToString("ddd, MMM d", CultureInfo.InvariantCulture)} " +
                $"{line.StartTime.ToString("h:mm tt", CultureInfo.InvariantCulture)}-{line.EndTime.ToString("h:mm tt", CultureInfo.InvariantCulture)} " +
                (string.IsNullOrWhiteSpace(line.Room) ? string.Empty : $"Room {line.Room} ") +
                $"(max {line.MaxMarks.ToString("0.##", CultureInfo.InvariantCulture)})");
        }

        textBody.AppendLine()
            .AppendLine($"View full schedule: {viewUrl}")
            .Append(EmailLayout.TextFooter(school));

        return new EmailContent(
            Subject: $"[{school.Name}] Exam schedule: {examName}",
            Html: html,
            Text: textBody.ToString());
    }

    /// <summary>
    /// Exam result finalized email. Shows the student's per-subject result
    /// row plus overall total/percentage. Sent to the specific student's
    /// parents (not the whole class) when results are finalized.
    /// </summary>
    public static EmailContent BuildExamResult(
        SchoolEntity school,
        string parentName,
        string studentName,
        string examName,
        string className,
        string section,
        DateTimeOffset finalizedAt,
        IEnumerable<ExamResultSubjectLine> subjects,
        string viewUrl,
        string logoUrl)
    {
        ArgumentNullException.ThrowIfNull(school);

        var safeParent = WebUtility.HtmlEncode(parentName);
        var safeStudent = WebUtility.HtmlEncode(studentName);
        var safeExam = WebUtility.HtmlEncode(examName);
        var safeClass = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(section) ? className : $"{className} - {section}");

        var finalizedText = finalizedAt
            .ToLocalTime()
            .ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

        var lines = subjects.ToList();

        var obtained = lines.Where(l => !l.IsAbsent && l.MarksObtained.HasValue).Sum(l => l.MarksObtained!.Value);
        var possible = lines.Sum(l => l.MaxMarks);
        var percentage = possible > 0 ? (double)(obtained / possible) * 100.0 : 0.0;

        var tableRows = new StringBuilder();
        tableRows.Append("""
                         <tr style="background-color:#F0FDF4;">
                             <th align="left" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#166534;border-bottom:1px solid #E5E7EB;">Subject</th>
                             <th align="right" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#166534;border-bottom:1px solid #E5E7EB;">Marks</th>
                             <th align="right" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#166534;border-bottom:1px solid #E5E7EB;">Max</th>
                             <th align="center" style="padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#166534;border-bottom:1px solid #E5E7EB;">Grade</th>
                         </tr>
                         """);

        foreach (var line in lines)
        {
            var marksCell = line.IsAbsent
                ? "<span style=\"color:#B91C1C;font-weight:600;\">Absent</span>"
                : (line.MarksObtained.HasValue
                    ? line.MarksObtained.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : "—");
            var grade = string.IsNullOrWhiteSpace(line.Grade) ? "—" : WebUtility.HtmlEncode(line.Grade);

            tableRows.Append($"""
                              <tr>
                                  <td style="padding:10px 12px;font-size:14px;color:#1F2937;border-bottom:1px solid #E5E7EB;">{WebUtility.HtmlEncode(line.Subject)}</td>
                                  <td align="right" style="padding:10px 12px;font-size:14px;color:#1F2937;border-bottom:1px solid #E5E7EB;">{marksCell}</td>
                                  <td align="right" style="padding:10px 12px;font-size:14px;color:#6B7280;border-bottom:1px solid #E5E7EB;">{line.MaxMarks.ToString("0.##", CultureInfo.InvariantCulture)}</td>
                                  <td align="center" style="padding:10px 12px;font-size:14px;color:#1F2937;border-bottom:1px solid #E5E7EB;">{grade}</td>
                              </tr>
                              """);
        }

        // Summary row
        tableRows.Append($"""
                         <tr style="background-color:#F9FAFB;font-weight:600;">
                             <td style="padding:10px 12px;font-size:14px;color:#1F2937;">Total</td>
                             <td align="right" style="padding:10px 12px;font-size:14px;color:#1F2937;">{obtained.ToString("0.##", CultureInfo.InvariantCulture)}</td>
                             <td align="right" style="padding:10px 12px;font-size:14px;color:#6B7280;">{possible.ToString("0.##", CultureInfo.InvariantCulture)}</td>
                             <td align="center" style="padding:10px 12px;font-size:14px;color:#4338CA;">{percentage.ToString("0.0", CultureInfo.InvariantCulture)}%</td>
                         </tr>
                         """);

        var inner = new StringBuilder();
        inner.Append(EmailLayout.Paragraph($"Hi <strong>{safeParent}</strong>,"));
        inner.Append(EmailLayout.Paragraph(
            $"The results for <strong>{safeExam}</strong> have been finalized. Here's a summary for <strong>{safeStudent}</strong>."));

        inner.Append(EmailLayout.DetailBlock(new List<(string, string)>
        {
            ("Student", studentName),
            ("Class", string.IsNullOrWhiteSpace(section) ? className : $"{className} - {section}"),
            ("Exam", examName),
            ("Finalized", finalizedText),
        }));

        inner.Append($"""
                     <div style="margin:20px 0;overflow:auto;">
                         <table width="100%" cellpadding="0" cellspacing="0" role="presentation" style="border-collapse:collapse;border:1px solid #E5E7EB;border-radius:12px;overflow:hidden;">
                             {tableRows}
                         </table>
                     </div>
                     """);

        inner.Append(EmailLayout.Button(viewUrl, "View full results in EduConnect"));
        inner.Append(EmailLayout.Paragraph(
            "If you have questions about any subject result, please contact the class teacher."));

        var html = EmailLayout.RenderLayout(
            school,
            logoUrl,
            preheader: $"{studentName} - {examName} results: {percentage.ToString("0.0", CultureInfo.InvariantCulture)}% ({obtained.ToString("0.##", CultureInfo.InvariantCulture)}/{possible.ToString("0.##", CultureInfo.InvariantCulture)})",
            innerHtml: inner.ToString());

        var textBody = new StringBuilder()
            .AppendLine($"Hi {parentName},")
            .AppendLine()
            .AppendLine($"The results for {examName} have been finalized. Summary for {studentName}:")
            .AppendLine()
            .AppendLine($"Student:    {studentName}")
            .AppendLine($"Class:      {(string.IsNullOrWhiteSpace(section) ? className : $"{className} - {section}")}")
            .AppendLine($"Exam:       {examName}")
            .AppendLine($"Finalized:  {finalizedText}")
            .AppendLine()
            .AppendLine("Results:");

        foreach (var line in lines)
        {
            var marksStr = line.IsAbsent
                ? "Absent"
                : (line.MarksObtained.HasValue
                    ? line.MarksObtained.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : "—");
            var gradeStr = string.IsNullOrWhiteSpace(line.Grade) ? string.Empty : $" [{line.Grade}]";
            textBody.AppendLine(
                $"  - {line.Subject}: {marksStr} / {line.MaxMarks.ToString("0.##", CultureInfo.InvariantCulture)}{gradeStr}");
        }

        textBody.AppendLine()
            .AppendLine($"Total: {obtained.ToString("0.##", CultureInfo.InvariantCulture)} / {possible.ToString("0.##", CultureInfo.InvariantCulture)} ({percentage.ToString("0.0", CultureInfo.InvariantCulture)}%)")
            .AppendLine()
            .AppendLine($"View full results: {viewUrl}")
            .Append(EmailLayout.TextFooter(school));

        return new EmailContent(
            Subject: $"[{school.Name}] Exam results: {examName} - {studentName}",
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

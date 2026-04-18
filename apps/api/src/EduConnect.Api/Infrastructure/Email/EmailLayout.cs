using System.Net;
using System.Text;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Infrastructure.Email;

/// <summary>
/// Renders the shared "card-style" HTML shell used by every transactional
/// email in EduConnect. The shell is fully inline-styled and table-free
/// (flexbox is avoided intentionally) so it renders consistently across
/// Gmail, Outlook, Apple Mail, and mobile clients.
/// </summary>
public static class EmailLayout
{
    /// <summary>Soft page background behind the card.</summary>
    private const string PageBackground = "#F4F6F9";

    /// <summary>White card surface.</summary>
    private const string CardBackground = "#FFFFFF";

    /// <summary>Primary brand color — used for the top accent bar and buttons.</summary>
    private const string PrimaryColor = "#4F46E5";

    /// <summary>Dark neutral for body text.</summary>
    private const string TextColor = "#1F2937";

    /// <summary>Muted neutral for secondary text / footer.</summary>
    private const string MutedColor = "#6B7280";

    /// <summary>Subtle border color for dividers.</summary>
    private const string BorderColor = "#E5E7EB";

    /// <summary>Cross-platform font stack; no external webfonts (many clients strip them).</summary>
    private const string FontStack =
        "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif";

    /// <summary>
    /// Wrap a block of inner HTML in the full document shell.
    /// <paramref name="preheader"/> is the hidden preview text most clients
    /// show next to the subject in the inbox list.
    /// </summary>
    public static string RenderLayout(
        SchoolEntity school,
        string logoUrl,
        string preheader,
        string innerHtml)
    {
        ArgumentNullException.ThrowIfNull(school);

        var schoolName = HtmlEncode(school.Name);
        var schoolAddress = HtmlEncode(school.Address);
        var contactEmail = HtmlEncode(school.ContactEmail);
        var contactPhone = HtmlEncode(school.ContactPhone);
        var safeLogoUrl = HtmlEncode(logoUrl);
        var safePreheader = HtmlEncode(preheader);

        var footerLines = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(school.Address))
        {
            footerLines.Append($"<div style=\"margin-top:6px;\">{schoolAddress}</div>");
        }

        var contactParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(school.ContactEmail))
        {
            contactParts.Add($"<a href=\"mailto:{contactEmail}\" style=\"color:{MutedColor};text-decoration:underline;\">{contactEmail}</a>");
        }
        if (!string.IsNullOrWhiteSpace(school.ContactPhone))
        {
            contactParts.Add(contactPhone);
        }
        if (contactParts.Count > 0)
        {
            footerLines.Append($"<div style=\"margin-top:4px;\">{string.Join(" &middot; ", contactParts)}</div>");
        }

        return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="UTF-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <meta name="x-apple-disable-message-reformatting" />
                    <title>{schoolName}</title>
                </head>
                <body style="margin:0;padding:0;background-color:{PageBackground};font-family:{FontStack};color:{TextColor};">
                    <div style="display:none;font-size:1px;color:{PageBackground};line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">
                        {safePreheader}
                    </div>
                    <div style="background-color:{PageBackground};padding:32px 16px;">
                        <div style="max-width:600px;margin:0 auto;background-color:{CardBackground};border-radius:16px;overflow:hidden;box-shadow:0 4px 16px rgba(15,23,42,0.06);border:1px solid {BorderColor};">
                            <div style="height:6px;background-color:{PrimaryColor};"></div>
                            <div style="padding:28px 32px 12px 32px;text-align:center;border-bottom:1px solid {BorderColor};">
                                <img src="{safeLogoUrl}" alt="{schoolName}" height="56" style="display:inline-block;height:56px;max-height:56px;border:0;outline:none;text-decoration:none;" />
                                <div style="margin-top:12px;font-size:20px;font-weight:600;color:{TextColor};letter-spacing:-0.01em;">
                                    {schoolName}
                                </div>
                            </div>
                            <div style="padding:28px 32px;font-size:15px;line-height:1.6;color:{TextColor};">
                                {innerHtml}
                            </div>
                            <div style="padding:20px 32px 28px 32px;border-top:1px solid {BorderColor};background-color:#FAFAFB;color:{MutedColor};font-size:12px;line-height:1.5;text-align:center;">
                                <div style="font-weight:600;color:{TextColor};">{schoolName}</div>
                                {footerLines}
                                <div style="margin-top:10px;color:{MutedColor};">
                                    This is an automated message from EduConnect. Please do not reply directly.
                                </div>
                            </div>
                        </div>
                    </div>
                </body>
                </html>
                """;
    }

    /// <summary>
    /// Build an inline-styled primary button linking to <paramref name="href"/>.
    /// </summary>
    public static string Button(string href, string label)
    {
        var safeHref = HtmlEncode(href);
        var safeLabel = HtmlEncode(label);
        return $"""
                <div style="margin:24px 0;text-align:center;">
                    <a href="{safeHref}" style="display:inline-block;background-color:{PrimaryColor};color:#FFFFFF;text-decoration:none;font-weight:600;font-size:15px;padding:12px 28px;border-radius:10px;">
                        {safeLabel}
                    </a>
                </div>
                """;
    }

    /// <summary>
    /// Render a highlighted credential pill (used for temp passwords / PINs).
    /// </summary>
    public static string CredentialBox(string label, string value)
    {
        var safeLabel = HtmlEncode(label);
        var safeValue = HtmlEncode(value);
        return $"""
                <div style="margin:20px 0;padding:16px 20px;background-color:#FFFBEB;border:1px solid #FCD34D;border-radius:12px;">
                    <div style="font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:#92400E;font-weight:600;">
                        {safeLabel}
                    </div>
                    <div style="margin-top:6px;font-family:'SFMono-Regular',Consolas,'Liberation Mono',Menlo,monospace;font-size:18px;font-weight:600;color:#78350F;letter-spacing:0.02em;">
                        {safeValue}
                    </div>
                </div>
                """;
    }

    /// <summary>
    /// Render a key/value detail row block (used for absence meta, notice meta).
    /// <paramref name="rows"/> is a list of (label, value) tuples.
    /// </summary>
    public static string DetailBlock(IEnumerable<(string Label, string Value)> rows)
    {
        var sb = new StringBuilder();
        sb.Append($"""<div style="margin:20px 0;padding:16px 20px;background-color:#F9FAFB;border:1px solid {BorderColor};border-radius:12px;">""");
        var first = true;
        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var topBorder = first
                ? string.Empty
                : $"border-top:1px solid {BorderColor};";
            var padding = first ? "padding:0 0 10px 0;" : "padding:10px 0;";

            sb.Append($"""
                      <div style="{topBorder}{padding}">
                          <div style="font-size:12px;text-transform:uppercase;letter-spacing:0.04em;color:{MutedColor};font-weight:600;">{HtmlEncode(label)}</div>
                          <div style="margin-top:4px;font-size:15px;color:{TextColor};">{HtmlEncode(value)}</div>
                      </div>
                      """);
            first = false;
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>
    /// Paragraph of body copy. Pre-encoded HTML is allowed in <paramref name="html"/>
    /// so callers can interpolate tags like &lt;strong&gt;.
    /// </summary>
    public static string Paragraph(string html)
    {
        return $"<p style=\"margin:0 0 14px 0;font-size:15px;line-height:1.6;color:{TextColor};\">{html}</p>";
    }

    /// <summary>
    /// Plain-text footer used by the text alternates to give recipients
    /// the school's contact info.
    /// </summary>
    public static string TextFooter(SchoolEntity school)
    {
        ArgumentNullException.ThrowIfNull(school);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--");
        sb.AppendLine(school.Name);
        if (!string.IsNullOrWhiteSpace(school.Address))
        {
            sb.AppendLine(school.Address);
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(school.ContactEmail))
        {
            parts.Add(school.ContactEmail);
        }
        if (!string.IsNullOrWhiteSpace(school.ContactPhone))
        {
            parts.Add(school.ContactPhone);
        }
        if (parts.Count > 0)
        {
            sb.AppendLine(string.Join(" | ", parts));
        }

        sb.AppendLine("This is an automated message from EduConnect.");
        return sb.ToString();
    }

    private static string HtmlEncode(string? value)
    {
        return value is null ? string.Empty : WebUtility.HtmlEncode(value);
    }
}

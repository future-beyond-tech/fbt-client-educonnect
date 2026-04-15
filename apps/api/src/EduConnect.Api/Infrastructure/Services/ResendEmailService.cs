using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EduConnect.Api.Common.Logging;

namespace EduConnect.Api.Infrastructure.Services;

/// <summary>
/// Sends transactional email via Resend (https://resend.com).
/// Configured through environment variables:
///   RESEND_API_KEY        — required
///   RESEND_FROM_EMAIL     — required, e.g. "EduConnect &lt;no-reply@your-domain.com&gt;"
/// </summary>
public class ResendEmailService : IEmailService
{
    private const string ResendEmailsEndpoint = "https://api.resend.com/emails";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["RESEND_API_KEY"];
        var fromEmail = _configuration["RESEND_FROM_EMAIL"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogError(
                "Resend is not configured (RESEND_API_KEY / RESEND_FROM_EMAIL missing). Skipping email (toMasked={ToMasked})",
                LogRedaction.MaskEmail(toEmail));
            return false;
        }

        var payload = new ResendEmailRequest
        {
            From = fromEmail,
            To = new[] { toEmail },
            Subject = subject,
            Html = htmlBody,
            Text = textBody
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ResendEmailsEndpoint)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Resend API returned {StatusCode} when sending email (toMasked={ToMasked}): {Body}",
                    (int)response.StatusCode,
                    LogRedaction.MaskEmail(toEmail),
                    body);
                return false;
            }

            _logger.LogInformation(
                "Sent transactional email via Resend (toMasked={ToMasked}, subject={Subject})",
                LogRedaction.MaskEmail(toEmail),
                subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Resend email (toMasked={ToMasked})", LogRedaction.MaskEmail(toEmail));
            return false;
        }
    }

    private sealed class ResendEmailRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string[] To { get; set; } = Array.Empty<string>();

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("html")]
        public string Html { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}

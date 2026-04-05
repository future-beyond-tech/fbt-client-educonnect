namespace EduConnect.Api.Common.Models;

public class ProblemDetailsResponse
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    public ProblemDetailsResponse() { }

    public ProblemDetailsResponse(int status, string? title, string? detail, string? instance = null, string? traceId = null)
    {
        Status = status;
        Title = title;
        Detail = detail;
        Instance = instance;
        TraceId = traceId;
        Type = $"https://httpstatuses.com/{status}";
    }
}

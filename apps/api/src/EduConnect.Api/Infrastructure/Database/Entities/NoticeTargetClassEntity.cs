namespace EduConnect.Api.Infrastructure.Database.Entities;

public class NoticeTargetClassEntity
{
    public Guid NoticeId { get; set; }
    public Guid ClassId { get; set; }
    public Guid SchoolId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public NoticeEntity? Notice { get; set; }
    public ClassEntity? TargetClass { get; set; }
    public SchoolEntity? School { get; set; }
}

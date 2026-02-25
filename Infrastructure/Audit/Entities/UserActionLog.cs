using System;

namespace BonyadRazi.Portal.Infrastructure.Audit.Entities;

public sealed class UserActionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Utc { get; set; } = DateTime.UtcNow;

    public Guid? UserId { get; set; }

    public string ActionType { get; set; } = default!;

    public string MetadataJson { get; set; } = "{}";

    public string? TraceId { get; set; }

    public int? StatusCode { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }

    public string? RemoteIp { get; set; }
    public string? UserAgent { get; set; }

    public Guid? CompanyCode { get; set; }

    public string? Reason { get; set; }
}

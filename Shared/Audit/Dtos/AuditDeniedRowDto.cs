namespace BonyadRazi.Portal.Shared.Audit.Dtos;

public sealed class AuditDeniedRowDto
{
    public DateTime Utc { get; init; }
    public int? StatusCode { get; init; }
    public Guid? UserId { get; init; }
    public Guid? CompanyCode { get; init; }
    public string ActionType { get; init; } = default!;
    public string? Reason { get; init; }
    public string? Method { get; init; }
    public string? Path { get; init; }
    public string? RemoteIp { get; init; }
    public string? UserAgent { get; init; }
    public string? TraceId { get; init; }
}
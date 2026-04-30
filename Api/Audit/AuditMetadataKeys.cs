namespace BonyadRazi.Portal.Api.Audit;

public static class AuditMetadataKeys
{
    public const string Method = "method";
    public const string Path = "path";
    public const string QueryString = "queryString";

    public const string RemoteIp = "remoteIp";
    public const string UserAgent = "userAgent";
    public const string TraceId = "traceId";
    public const string CorrelationId = "correlationId";

    public const string StatusCode = "statusCode";
    public const string Reason = "reason";

    public const string UserId = "userId";
    public const string Username = "username";
    public const string CompanyCode = "companyCode";

    public const string RouteCompanyCode = "routeCompanyCode";
    public const string ClaimCompanyCode = "claimCompanyCode";

    public const string RefreshTokenId = "refreshTokenId";
    public const string ReplacedByTokenId = "replacedByTokenId";
    public const string RevokeReason = "revokeReason";

    public const string RateLimitBucket = "rateLimitBucket";
    public const string RetryAfterSeconds = "retryAfterSeconds";
}
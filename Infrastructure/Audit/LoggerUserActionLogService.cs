using System.Text.Json;
using System.Text.Json.Serialization;
using BonyadRazi.Portal.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BonyadRazi.Portal.Infrastructure.Audit;

public sealed class LoggerUserActionLogService : IUserActionLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<LoggerUserActionLogService> _logger;

    public LoggerUserActionLogService(ILogger<LoggerUserActionLogService> logger) => _logger = logger;

    public Task LogAsync(Guid? userId, string actionType, object metadata, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(metadata ?? new { }, JsonOptions);
        _logger.LogInformation("AUDIT {ActionType} UserId={UserId} Metadata={Metadata}", actionType, userId, json);
        return Task.CompletedTask;
    }
}
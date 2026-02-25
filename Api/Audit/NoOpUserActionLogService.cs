using BonyadRazi.Portal.Application.Abstractions;

namespace BonyadRazi.Portal.Api.Audit;

public sealed class NoOpUserActionLogService : IUserActionLogService
{
    public Task LogAsync(Guid? userId, string actionType, object metadata, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
using ContentServer.Domain.Administration;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Reviews;

public partial record ReviewRecordId : IGuidStronglyTypedId;

public sealed class ReviewRecord : Entity<ReviewRecordId>, IAggregateRoot
{
    private ReviewRecord()
    {
    }

    public AdministratorId AdministratorId { get; private set; } = null!;

    public string TargetType { get; private set; } = string.Empty;

    public string TargetId { get; private set; } = string.Empty;

    public string Decision { get; private set; } = string.Empty;

    public string? Message { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ReviewRecord Create(
        AdministratorId administratorId,
        string targetType,
        string targetId,
        string decision,
        string? message,
        DateTimeOffset now)
    {
        return new ReviewRecord
        {
            AdministratorId = administratorId,
            TargetType = targetType,
            TargetId = targetId,
            Decision = decision,
            Message = NormalizeOptionalText(message),
            CreatedAt = now
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

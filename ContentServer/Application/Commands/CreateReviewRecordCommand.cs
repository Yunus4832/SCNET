using ContentServer.Domain.Administration;
using ContentServer.Domain.Reviews;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record CreateReviewRecordCommand(
    AdministratorId AdministratorId,
    string TargetType,
    string TargetId,
    string Decision,
    string? Message,
    DateTimeOffset OccurredAt) : ICommand;

public sealed class CreateReviewRecordCommandHandler(ReviewRecordRepository repository)
    : ICommandHandler<CreateReviewRecordCommand>
{
    public Task Handle(CreateReviewRecordCommand command, CancellationToken cancellationToken)
    {
        return repository.AddAsync(ReviewRecord.Create(
            command.AdministratorId,
            command.TargetType,
            command.TargetId,
            command.Decision,
            command.Message,
            command.OccurredAt), cancellationToken);
    }
}

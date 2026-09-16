using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerSources;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SubmitServerSourceCommand(PublisherId PublisherId, string Name, Uri ApiUrl, string? Description)
    : ICommand<SubmitServerSourceResult>;

public sealed record SubmitServerSourceResult(ServerSourceRegistrationId Id,
    ServerSourceRegistrationStatus Status, DateTimeOffset CreatedAt);

public sealed class SubmitServerSourceCommandHandler(ServerSourceRegistrationRepository repository)
    : ICommandHandler<SubmitServerSourceCommand, SubmitServerSourceResult>
{
    public async Task<SubmitServerSourceResult> Handle(SubmitServerSourceCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.HasPendingOrActiveUrlAsync(command.ApiUrl.AbsoluteUri, cancellationToken))
        {
            throw new InvalidOperationException("Server source URL is already registered.");
        }

        var source = ServerSourceRegistration.Submit(command.PublisherId, command.Name, command.ApiUrl,
            command.Description, DateTimeOffset.UtcNow);
        await repository.AddAsync(source, cancellationToken);
        return new SubmitServerSourceResult(source.Id, source.Status, source.CreatedAt);
    }
}

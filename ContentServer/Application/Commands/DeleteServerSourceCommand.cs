using ContentServer.Domain.ServerSources;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DeleteServerSourceCommand(ServerSourceRegistrationId Id) : ICommand<bool>;

public sealed class DeleteServerSourceCommandHandler(ServerSourceRegistrationRepository repository)
    : ICommandHandler<DeleteServerSourceCommand, bool>
{
    public async Task<bool> Handle(DeleteServerSourceCommand command, CancellationToken cancellationToken)
    {
        var source = await repository.FindAsync(command.Id, cancellationToken);
        if (source is null)
        {
            return false;
        }

        repository.Delete(source);
        return true;
    }
}

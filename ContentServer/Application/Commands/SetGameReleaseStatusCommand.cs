using ContentServer.Domain.Releases;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SetGameReleaseStatusCommand(GameReleaseId Id, bool Publish) : ICommand<bool>;

public sealed class SetGameReleaseStatusCommandHandler(ContentServerDbContext db)
    : ICommandHandler<SetGameReleaseStatusCommand, bool>
{
    public async Task<bool> Handle(SetGameReleaseStatusCommand command, CancellationToken cancellationToken)
    {
        var release = await db.GameReleases.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (release is null)
        {
            return false;
        }

        if (command.Publish)
        {
            release.Publish(DateTimeOffset.UtcNow);
        }
        else
        {
            release.Withdraw(DateTimeOffset.UtcNow);
        }

        return true;
    }
}

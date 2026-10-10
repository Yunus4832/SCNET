using ContentServer.Domain.Releases;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record EditGameReleaseCommand(GameReleaseId Id, string Version, string Description,
    IReadOnlyList<GameReleaseArtifactData> Artifacts) : ICommand<bool>;

public sealed class EditGameReleaseCommandHandler(ContentServerDbContext db)
    : ICommandHandler<EditGameReleaseCommand, bool>
{
    public async Task<bool> Handle(EditGameReleaseCommand command, CancellationToken cancellationToken)
    {
        var release = await db.GameReleases.Include(item => item.Artifacts)
            .FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (release is null)
        {
            return false;
        }

        if (await db.GameReleases.AnyAsync(item => item.Id != command.Id && item.Version == command.Version,
                cancellationToken))
        {
            throw new InvalidOperationException("A release already exists for this version and platform.");
        }

        release.Edit(command.Version, command.Description, command.Artifacts, DateTimeOffset.UtcNow);
        return true;
    }
}

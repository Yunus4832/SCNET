using ContentServer.Domain.Releases;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record CreateGameReleaseCommand(
    string Version,
    string Description,
    IReadOnlyList<GameReleaseArtifactData> Artifacts) : ICommand<GameReleaseId>;

public sealed class CreateGameReleaseCommandHandler(GameReleaseRepository repository, ContentServerDbContext db)
    : ICommandHandler<CreateGameReleaseCommand, GameReleaseId>
{
    public async Task<GameReleaseId> Handle(CreateGameReleaseCommand command, CancellationToken cancellationToken)
    {
        if (await db.GameReleases.AnyAsync(item => item.Version == command.Version, cancellationToken))
        {
            throw new InvalidOperationException("A release already exists for this version and platform.");
        }

        var release = GameRelease.Create(command.Version, command.Description, command.Artifacts,
            DateTimeOffset.UtcNow);
        await repository.AddAsync(release, cancellationToken);
        return release.Id;
    }
}

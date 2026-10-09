using ContentServer.Domain.Contents;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DeleteUnreferencedPackagesCommand(IReadOnlyCollection<string> Hashes) : ICommand;

public sealed class DeleteUnreferencedPackagesCommandHandler(ContentServerDbContext db)
    : ICommandHandler<DeleteUnreferencedPackagesCommand>
{
    public async Task Handle(DeleteUnreferencedPackagesCommand command, CancellationToken cancellationToken)
    {
        var packages = await db.PackageBlobs.Where(package => command.Hashes.Contains(package.Hash))
            .ToListAsync(cancellationToken);
        foreach (var package in packages)
        {
            if (!await db.ContentVersions.AnyAsync(version => version.PackageBlobId == package.Id,
                    cancellationToken))
            {
                package.Delete();
            }
        }
    }
}

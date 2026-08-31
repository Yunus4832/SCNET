using ContentServer.Domain.Contents;
using ContentServer.Domain.Packages;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SubmitContentPackageCommand(
    PublisherId PublisherId,
    string Type,
    string Identifier,
    string Name,
    string? Summary,
    string Version,
    string Metadata,
    string PackageHash,
    string BlobHash,
    long PackageSize,
    string FileName,
    string MediaType) : ICommand;

public sealed class SubmitContentPackageCommandHandler(ContentServerDbContext db)
    : ICommandHandler<SubmitContentPackageCommand>
{
    public async Task Handle(SubmitContentPackageCommand command, CancellationToken cancellationToken)
    {
        var package = await db.PackageBlobs.SingleOrDefaultAsync(
            item => item.Hash == command.PackageHash, cancellationToken);
        if (package is null)
        {
            package = PackageBlob.Create(command.PackageHash, command.BlobHash, command.PackageSize,
                command.FileName, command.MediaType, DateTimeOffset.UtcNow);
            await db.PackageBlobs.AddAsync(package, cancellationToken);
        }

        var normalizedIdentifier = command.Identifier.ToLowerInvariant();
        var content = await db.Contents.Include(item => item.Versions).SingleOrDefaultAsync(
            item => item.NormalizedIdentifier == normalizedIdentifier, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (content is null)
        {
            content = ContentItem.Create(command.PublisherId, command.Type, command.Identifier,
                command.Name, command.Summary, now);
            await db.Contents.AddAsync(content, cancellationToken);
        }
        else
        {
            if (content.PublisherId != command.PublisherId)
                throw new KnownException("identifier_not_owned", 403);
            if (content.Type != command.Type)
                throw new KnownException("content_type_conflict", 409);
            if (content.Versions.Any(item => item.Version == command.Version))
                throw new KnownException("content_version_conflict", 409);
            content.UpdateDetails(command.Name, command.Summary, now);
        }

        content.SubmitVersion(command.Version, package.Id, command.PackageHash, command.BlobHash,
            command.Metadata, now);
    }
}

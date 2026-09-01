using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using MediatR;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application;

public sealed record ContentPackageSubmissionResult(ContentVersionDto Version, bool Created);

public sealed class ContentPackageSubmissionService(
    IMediator mediator,
    ContentPackageStore packageStore,
    ContentSubmissionLock submissionLock)
{
    public async Task<ContentPackageSubmissionResult> SubmitAsync(
        PublisherId publisherId,
        StagedContentPackage staged,
        string? summary,
        CancellationToken cancellationToken)
    {
        IDisposable lease;
        try
        {
            lease = await submissionLock.EnterAsync(cancellationToken);
        }
        catch
        {
            packageStore.DeleteTemporary(staged);
            throw;
        }

        using (lease)
        {
            var manifest = staged.Inspection.Manifest;
            var type = manifest.Type.ToString();
            var existingContent = await mediator.Send(
                new FindContentItemQuery(publisherId, manifest.Identifier), cancellationToken);
            if (existingContent is not null && existingContent.PublisherId != publisherId)
            {
                packageStore.DeleteTemporary(staged);
                throw new KnownException("identifier_not_owned", 403);
            }
            if (existingContent is not null && existingContent.Type != type)
            {
                packageStore.DeleteTemporary(staged);
                throw new KnownException("content_type_conflict", 409);
            }

            var existingVersion = await mediator.Send(
                new GetContentVersionQuery(publisherId, manifest.Identifier, manifest.Version), cancellationToken);
            if (existingVersion is not null)
            {
                packageStore.DeleteTemporary(staged);
                if (existingVersion.PackageHash != staged.Inspection.PackageHash)
                    throw new KnownException("content_version_conflict", 409);
                return new ContentPackageSubmissionResult(existingVersion, false);
            }

            packageStore.Commit(staged);
            await mediator.Send(new SubmitContentPackageCommand(
                publisherId, type, manifest.Identifier, manifest.Name, summary, manifest.Version,
                manifest.Metadata.GetRawText(), staged.Inspection.PackageHash, staged.BlobHash,
                staged.Size, staged.FileName, staged.MediaType), cancellationToken);

            var submitted = await mediator.Send(
                new GetContentVersionQuery(publisherId, manifest.Identifier, manifest.Version), cancellationToken)
                ?? throw new KnownException("content_version_not_found", 500);
            return new ContentPackageSubmissionResult(submitted, true);
        }
    }
}

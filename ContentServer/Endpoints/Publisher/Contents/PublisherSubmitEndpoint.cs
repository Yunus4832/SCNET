using Content.Packaging;

using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Endpoints.Public.Contents;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Contents;

[HttpPost("/api/v1/publisher/submissions")]
[AllowAnonymous]
[AllowFileUploads(dontAutoBindFormData: true)]
public sealed class PublisherSubmitEndpoint(
    IMediator mediator,
    IOptions<ContentServerOptions> options,
    ApiKeyAuthenticationContext authenticationContext,
    ContentPackageStore packageStore,
    ContentSubmissionLock submissionLock) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await Submit(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ContentVersionResponse>> Submit(CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        if (!HttpContext.Request.HasFormContentType)
        {
            throw new KnownException("form_required", StatusCodes.Status400BadRequest);
        }

        var form = await HttpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("package");
        if (file is null || file.Length == 0 || file.Length > options.Value.MaximumPackageBytes)
        {
            throw new KnownException("invalid_submission", StatusCodes.Status400BadRequest);
        }

        StagedContentPackage staged;
        try
        {
            await using var input = file.OpenReadStream();
            staged = await packageStore.StageAsync(input, file.FileName,
                "application/vnd.scnet.content-package", options.Value.MaximumPackageBytes, cancellationToken);
        }
        catch (ContentPackageException)
        {
            throw new KnownException("invalid_content_package", StatusCodes.Status400BadRequest);
        }

        var result = await SubmitStagedPackageAsync(
            publisher.PublisherId, staged, cancellationToken);
        HttpContext.Response.StatusCode = result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        return result.Version
            .ToResponse()
            .AsResponseData(code: HttpContext.Response.StatusCode);
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new GetPublisherQuery(authenticationContext.RequirePublisherId()),
            cancellationToken
        ) ?? throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }

    private async Task<(ContentVersionDto Version, bool Created)> SubmitStagedPackageAsync(
        PublisherId publisherId, StagedContentPackage staged, CancellationToken cancellationToken)
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
                {
                    throw new KnownException("content_version_conflict", 409);
                }

                return (existingVersion, false);
            }

            packageStore.Commit(staged);
            await mediator.Send(new SubmitContentPackageCommand(
                publisherId, type, manifest.Identifier, manifest.Name, manifest.Summary, manifest.Description,
                manifest.Version, manifest.Metadata.GetRawText(), staged.Inspection.PackageHash, staged.BlobHash,
                staged.Size, staged.FileName, staged.MediaType), cancellationToken);

            var submitted = await mediator.Send(
                                new GetContentVersionQuery(publisherId, manifest.Identifier, manifest.Version),
                                cancellationToken)
                            ?? throw new KnownException("content_version_not_found", 500);
            return (submitted, true);
        }
    }
}

using Content.Packaging;

using ContentServer.Application;
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

namespace ContentServer.Endpoints.Publisher.Packages;

[HttpPost("/api/v1/publisher/packages/image/submit")]
[AllowAnonymous]
[AllowFileUploads(dontAutoBindFormData: true)]
public sealed class PublisherPackageCreationSubmitImagePackageEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    IOptions<ContentServerOptions> options,
    ImageContentPackageBuilder imageBuilder,
    ContentSubmissionLock submissionLock,
    ContentPackageStore packageStore) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await SubmitImagePackage(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ContentVersionResponse>> SubmitImagePackage(
        CancellationToken cancellationToken)
    {
        var publisher = await RequireActivePublisherAsync(cancellationToken);
        var (form, file) = await RequireImageBuildFormAsync(cancellationToken);
        var staged = await BuildImagePackageAsync(form, file, cancellationToken);
        var result = await SubmitStagedPackageAsync(
            publisher.PublisherId, staged, cancellationToken);
        HttpContext.Response.StatusCode = result.Created ? 201 : 200;
        return result.Version.ToResponse().AsResponseData(code: HttpContext.Response.StatusCode);
    }

    private async Task<StagedContentPackage> BuildImagePackageAsync(
        IFormCollection form,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var input = file.OpenReadStream();
            return await imageBuilder.BuildAsync(input, ParseImageType(form["type"].ToString()),
                form["identifier"].ToString(), form["name"].ToString(),
                form["summary"].ToString(), form["description"].ToString(), form["version"].ToString(),
                options.Value.MaximumPackageBytes, cancellationToken);
        }
        catch (ContentPackageException)
        {
            throw new KnownException("invalid_image_package", 400);
        }
    }

    private async Task<(IFormCollection Form, IFormFile File)> RequireImageBuildFormAsync(
        CancellationToken cancellationToken)
    {
        if (!HttpContext.Request.HasFormContentType)
        {
            throw new KnownException("form_required", 400);
        }

        var form = await HttpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("source");
        if (file is null || file.Length == 0 || string.IsNullOrWhiteSpace(form["type"]) ||
            string.IsNullOrWhiteSpace(form["identifier"]) || string.IsNullOrWhiteSpace(form["name"]) ||
            string.IsNullOrWhiteSpace(form["version"]))
        {
            throw new KnownException("invalid_image_creation_request", 400);
        }

        return (form, file);
    }

    private async Task<PublisherDto> RequireActivePublisherAsync(CancellationToken cancellationToken)
    {
        var publisher = await mediator.Send(
                            new GetPublisherQuery(authenticationContext.RequirePublisherId()), cancellationToken)
                        ?? throw new KnownException("publisher_not_found", 401);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", 403);
        }

        return publisher;
    }

    private static ContentPackageType ParseImageType(string type) => type switch
    {
        "BlocksTexture" or "blocksTexture" => ContentPackageType.BlocksTexture,
        "CharacterSkin" or "characterSkin" => ContentPackageType.CharacterSkin,
        _ => throw new KnownException("unsupported_image_content_type", 400)
    };

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

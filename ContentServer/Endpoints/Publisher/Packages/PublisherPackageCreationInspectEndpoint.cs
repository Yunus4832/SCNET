using Content.Packaging;

using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Packages;

[FastEndpoints.HttpPost("/api/v1/publisher/packages/inspect")]
[AllowAnonymous]
[AllowFileUploads(dontAutoBindFormData: true)]
[RequestSizeLimit(268435456)]
public sealed class PublisherPackageCreationInspectEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    IOptions<ContentServerOptions> options,
    ContentPackageStore packageStore) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await Inspect(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PackagePreviewResponse>> Inspect(CancellationToken cancellationToken)
    {
        _ = await RequireActivePublisherAsync(cancellationToken);
        var file = await RequireFormFileAsync("package", cancellationToken);
        StagedContentPackage staged;
        try
        {
            await using var input = file.OpenReadStream();
            staged = await packageStore.StageAsync(input, file.FileName,
                "application/vnd.scnet.content-package", options.Value.MaximumPackageBytes, cancellationToken);
        }
        catch (ContentPackageException)
        {
            throw new KnownException("invalid_content_package", 400);
        }

        packageStore.DeleteTemporary(staged);
        var manifest = staged.Inspection.Manifest;
        return new PackagePreviewResponse(manifest.Type.ToString(), manifest.Identifier, manifest.Name,
            manifest.Summary, manifest.Description, manifest.Version, staged.Inspection.PackageHash, staged.Size,
            staged.Inspection.Entries).AsResponseData();
    }

    private async Task<IFormFile> RequireFormFileAsync(string name, CancellationToken cancellationToken)
    {
        if (!HttpContext.Request.HasFormContentType)
        {
            throw new KnownException("form_required", 400);
        }

        var form = await HttpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile(name);
        if (file is null || file.Length == 0 || file.Length > options.Value.MaximumPackageBytes)
        {
            throw new KnownException("invalid_file", 400);
        }

        return file;
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
}

public sealed record PackagePreviewResponse(
    string Type,
    string Identifier,
    string Name,
    string Summary,
    string Description,
    string Version,
    string PackageHash,
    long PackageSize,
    IReadOnlyList<ContentPackageEntry> Entries);

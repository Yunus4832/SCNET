using Content.Packaging;

using ContentServer.Application;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Mappings;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

using SixLabors.ImageSharp;

namespace ContentServer.Controllers;

public sealed record PackagePreviewResponse(
    string Type,
    string Identifier,
    string Name,
    string Version,
    string PackageHash,
    long PackageSize,
    IReadOnlyList<ContentPackageEntry> Entries);

[ApiController]
[Route("api/v1/publisher/packages")]
public sealed class PublisherPackageCreationController(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    IOptions<ContentServerOptions> options,
    ContentPackageStore packageStore,
    ImageContentPackageBuilder imageBuilder,
    ContentPackageSubmissionService submissionService) : ControllerBase
{
    [HttpPost("inspect")]
    [RequestSizeLimit(268435456)]
    public async Task<ResponseData<PackagePreviewResponse>> Inspect(CancellationToken cancellationToken)
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
            manifest.Version, staged.Inspection.PackageHash, staged.Size, staged.Inspection.Entries).AsResponseData();
    }

    [HttpPost("image/validate-source")]
    public async Task<ResponseData<ImageSourceInspection>> ValidateImageSource(CancellationToken cancellationToken)
    {
        _ = await RequireActivePublisherAsync(cancellationToken);
        if (!Request.HasFormContentType)
        {
            throw new KnownException("form_required", 400);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("source");
        if (file is null || file.Length == 0 || file.Length > options.Value.MaximumPackageBytes)
        {
            throw new KnownException("invalid_file", 400);
        }

        try
        {
            await using var input = file.OpenReadStream();
            return (await imageBuilder.ValidateSourceAsync(
                input, ParseImageType(form["type"].ToString()), options.Value.MaximumPackageBytes,
                cancellationToken)).AsResponseData();
        }
        catch (Exception exception) when (exception is ContentPackageException or UnknownImageFormatException)
        {
            throw new KnownException("invalid_png_source", 400);
        }
    }

    [HttpPost("image/build")]
    public async Task<IActionResult> BuildImagePackage(CancellationToken cancellationToken)
    {
        _ = await RequireActivePublisherAsync(cancellationToken);
        var (form, file) = await RequireImageBuildFormAsync(cancellationToken);
        var staged = await BuildImagePackageAsync(form, file, cancellationToken);
        var stream = new FileStream(staged.TemporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        return File(stream, staged.MediaType, staged.FileName, true);
    }

    [HttpPost("image/submit")]
    public async Task<ResponseData<Contracts.Responses.ContentVersionResponse>> SubmitImagePackage(
        CancellationToken cancellationToken)
    {
        var publisher = await RequireActivePublisherAsync(cancellationToken);
        var (form, file) = await RequireImageBuildFormAsync(cancellationToken);
        var staged = await BuildImagePackageAsync(form, file, cancellationToken);
        var result = await submissionService.SubmitAsync(
            publisher.PublisherId, staged, form["description"], cancellationToken);
        Response.StatusCode = result.Created ? 201 : 200;
        return result.Version.ToResponse().AsResponseData(code: Response.StatusCode);
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
                form["identifier"].ToString(), form["name"].ToString(), form["version"].ToString(),
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
        if (!Request.HasFormContentType)
        {
            throw new KnownException("form_required", 400);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("source");
        if (file is null || file.Length == 0 || string.IsNullOrWhiteSpace(form["type"]) ||
            string.IsNullOrWhiteSpace(form["identifier"]) || string.IsNullOrWhiteSpace(form["name"]) ||
            string.IsNullOrWhiteSpace(form["version"]))
        {
            throw new KnownException("invalid_image_creation_request", 400);
        }

        return (form, file);
    }

    private async Task<IFormFile> RequireFormFileAsync(string name, CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            throw new KnownException("form_required", 400);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
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

    private static ContentPackageType ParseImageType(string type) => type switch
    {
        "BlocksTexture" or "blocksTexture" => ContentPackageType.BlocksTexture,
        "CharacterSkin" or "characterSkin" => ContentPackageType.CharacterSkin,
        _ => throw new KnownException("unsupported_image_content_type", 400)
    };
}

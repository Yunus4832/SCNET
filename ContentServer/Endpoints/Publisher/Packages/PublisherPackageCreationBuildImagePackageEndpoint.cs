using Content.Packaging;

using ContentServer.Application;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Packages;

[HttpPost("/api/v1/publisher/packages/image/build")]
[AllowAnonymous]
[AllowFileUploads(dontAutoBindFormData: true)]
public sealed class PublisherPackageCreationBuildImagePackageEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    IOptions<ContentServerOptions> options,
    ImageContentPackageBuilder imageBuilder) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        _ = await RequireActivePublisherAsync(cancellationToken);
        var (form, file) = await RequireImageBuildFormAsync(cancellationToken);
        var staged = await BuildImagePackageAsync(form, file, cancellationToken);
        var stream = new FileStream(staged.TemporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        await Results.File(stream, staged.MediaType, staged.FileName,
            enableRangeProcessing: true).ExecuteAsync(HttpContext);
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
}

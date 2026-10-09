using Content.Packaging;

using ContentServer.Application;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

using SixLabors.ImageSharp;

namespace ContentServer.Endpoints.Publisher.Packages;

[HttpPost("/api/v1/publisher/packages/image/validate-source")]
[AllowAnonymous]
[AllowFileUploads(dontAutoBindFormData: true)]
public sealed class PublisherPackageCreationValidateImageSourceEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    IOptions<ContentServerOptions> options,
    ImageContentPackageBuilder imageBuilder) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await ValidateImageSource(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ImageSourceInspection>> ValidateImageSource(CancellationToken cancellationToken)
    {
        _ = await RequireActivePublisherAsync(cancellationToken);
        if (!HttpContext.Request.HasFormContentType)
        {
            throw new KnownException("form_required", 400);
        }

        var form = await HttpContext.Request.ReadFormAsync(cancellationToken);
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

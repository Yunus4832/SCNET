using Content.Packaging;

using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;
using ContentServer.Controllers.Mappings;
using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1/publisher")]
public sealed class PublisherController(
    IMediator mediator,
    IOptions<ContentServerOptions> options,
    ApiKeyAuthenticationContext authenticationContext,
    ContentPackageStore packageStore,
    ContentPackageSubmissionService submissionService) : ControllerBase
{
    [HttpGet]
    public async Task<ResponseData<PublisherResponse>> Self(CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        return publisher.ToResponse().AsResponseData();
    }

    [HttpGet("submissions")]
    public async Task<ResponseData<PagedData<ContentVersionResponse>>> Submissions(
        [FromQuery] string? query,
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var publisherId = authenticationContext.RequirePublisherId();
        var items = await mediator.Send(
            new ListVersionsQuery(
                VersionQueryScope.Publisher,
                page.ToPageRequest(),
                Search: query,
                PublisherId: publisherId),
            cancellationToken
        );
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }

    [HttpGet("content")]
    public async Task<ResponseData<PagedData<ContentItemResponse>>> Content(
        [FromQuery] string? query,
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListContentItemsQuery(
                page.ToPageRequest(),
                query,
                authenticationContext.RequirePublisherId()),
            cancellationToken);
        return items.Map(item => new ContentItemResponse(
            item.ContentId.ToString(),
            item.PublisherId.ToString(),
            item.Type,
            item.Identifier,
            item.Name,
            item.Summary,
            item.Status.ToString().ToLowerInvariant(),
            item.CreatedAt,
            item.UpdatedAt)).AsResponseData();
    }

    [HttpPost("content/{contentId}/disable")]
    public Task<ResponseData> DisableContent(string contentId, CancellationToken cancellationToken) =>
        SetContentStatus(contentId, ContentStatus.Disabled, cancellationToken);

    [HttpPost("content/{contentId}/enable")]
    public Task<ResponseData> EnableContent(string contentId, CancellationToken cancellationToken) =>
        SetContentStatus(contentId, ContentStatus.Active, cancellationToken);

    [HttpGet("submissions/{versionId}")]
    public async Task<ResponseData<ContentVersionResponse>> SubmissionStatus(
        string versionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(versionId, out var id))
        {
            throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
        }

        var item = await mediator.Send(new GetPublisherSubmissionQuery(
                       authenticationContext.RequirePublisherId(),
                       new ContentVersionId(id)), cancellationToken)
                   ?? throw new KnownException("submission_not_found", StatusCodes.Status404NotFound);
        return item.ToResponse().AsResponseData();
    }

    [HttpPost("submissions")]
    [RequestSizeLimit(268435456)]
    public async Task<ResponseData<ContentVersionResponse>> Submit(CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        if (!Request.HasFormContentType)
        {
            throw new KnownException("form_required", StatusCodes.Status400BadRequest);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
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

        var result = await submissionService.SubmitAsync(
            publisher.PublisherId, staged, form["summary"], cancellationToken);
        Response.StatusCode = result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        return result.Version
            .ToResponse()
            .AsResponseData(code: Response.StatusCode);
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new GetPublisherQuery(authenticationContext.RequirePublisherId()),
            cancellationToken
        ) ?? throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }

    private async Task<ResponseData> SetContentStatus(
        string contentId,
        ContentStatus status,
        CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        if (!Guid.TryParse(contentId, out var id))
        {
            throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
        }

        var result = await mediator.Send(
            new SetPublisherContentStatusCommand(
                new ContentId(id),
                publisher.PublisherId,
                status),
            cancellationToken);
        if (result == SetPublisherContentStatusResult.NotFound)
        {
            throw new KnownException("content_not_found", StatusCodes.Status404NotFound);
        }

        if (result == SetPublisherContentStatusResult.NotOwned)
        {
            throw new KnownException("content_not_owned", StatusCodes.Status403Forbidden);
        }

        return new ResponseData(true, string.Empty, StatusCodes.Status200OK, null);
    }
}

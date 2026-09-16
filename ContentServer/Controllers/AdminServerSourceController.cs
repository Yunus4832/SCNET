using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;
using ContentServer.Domain.ServerSources;
using ContentServer.Middlewares;

using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1/admin/server-sources")]
public sealed class AdminServerSourceController(IMediator mediator, IServerSourceInspectionService inspection,
    ApiKeyAuthenticationContext authenticationContext, IOptions<ContentServerOptions> options)
    : ControllerBase
{
    [HttpGet]
    public async Task<ResponseData<ServerSourceManagementResponse[]>> List(
        [FromQuery] ServerSourceRegistrationStatus? status, CancellationToken cancellationToken)
    {
        var sources = await mediator.Send(new ListServerSourcesQuery(status), cancellationToken);
        var responses = sources.Select(source => new ServerSourceManagementResponse(source.Id.ToString(),
            source.PublisherId.ToString(), source.PublisherName, source.Name, source.ApiUrl, source.Description,
            source.Status.ToString().ToLowerInvariant(), source.ReviewMessage, source.CreatedAt, source.ReviewedAt,
            false)).ToList();
        if (status is null && options.Value.BuiltInServerDirectoryEnabled)
        {
            responses.Insert(0, new ServerSourceManagementResponse("builtin", null, null,
                options.Value.BuiltInServerDirectoryName,
                ServerSourceController.GetBuiltInDirectoryUrl(options.Value, Request),
                options.Value.BuiltInServerDirectoryDescription, "active", null, null, null, true));
        }

        return responses.ToArray().AsResponseData();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ResponseData> Approve(Guid id, CancellationToken cancellationToken)
    {
        var source = await Find(id, cancellationToken);
        try
        {
            await inspection.InspectAsync(new Uri(source.ApiUrl), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                          ServerSource.Protocol.ServerSourceProtocolException)
        {
            throw new KnownException("server_source_unavailable_or_invalid", StatusCodes.Status422UnprocessableEntity);
        }

        await Review(id, true, null, cancellationToken);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ResponseData> Reject(Guid id, ReviewRequest request, CancellationToken cancellationToken)
    {
        await Review(id, false, request.Message, cancellationToken);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ResponseData> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await mediator.Send(new DeleteServerSourceCommand(new ServerSourceRegistrationId(id)),
                cancellationToken))
        {
            throw new KnownException("server_source_not_found", StatusCodes.Status404NotFound);
        }
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    private async Task<ServerSourceDto> Find(Guid id, CancellationToken cancellationToken)
    {
        var sources = await mediator.Send(new ListServerSourcesQuery(Id: new ServerSourceRegistrationId(id)),
            cancellationToken);
        return sources.FirstOrDefault()
               ?? throw new KnownException("server_source_not_found", StatusCodes.Status404NotFound);
    }

    private async Task Review(Guid id, bool approved, string? message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReviewServerSourceCommand(new ServerSourceRegistrationId(id),
            authenticationContext.RequireAdministratorId(), approved, message), cancellationToken);
        if (result == ReviewServerSourceResult.NotFound)
        {
            throw new KnownException("server_source_not_found", StatusCodes.Status404NotFound);
        }

        if (result == ReviewServerSourceResult.InvalidState)
        {
            throw new KnownException("server_source_already_reviewed", StatusCodes.Status409Conflict);
        }
    }
}

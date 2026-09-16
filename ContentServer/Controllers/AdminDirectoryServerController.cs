using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Middlewares;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1/admin/servers")]
public sealed class AdminDirectoryServerController(IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ControllerBase
{
    [HttpGet]
    public async Task<ResponseData<DirectoryServerResponse[]>> List(
        [FromQuery] DirectoryServerReviewStatus? status, CancellationToken cancellationToken)
    {
        var servers = await mediator.Send(new ListDirectoryServersQuery(ReviewStatus: status), cancellationToken);
        return servers.Select(ServerDirectoryController.Map).ToArray().AsResponseData();
    }

    [HttpPost("{id:guid}/approve")]
    public Task<ResponseData> Approve(Guid id, CancellationToken cancellationToken) =>
        Review(id, true, null, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    public Task<ResponseData> Reject(Guid id, ReviewRequest request, CancellationToken cancellationToken) =>
        Review(id, false, request.Message, cancellationToken);

    [HttpPost("{id:guid}/suspend")]
    public Task<ResponseData> Suspend(Guid id, ReviewRequest request, CancellationToken cancellationToken) =>
        SetSuspended(id, true, request.Message, cancellationToken);

    [HttpPost("{id:guid}/restore")]
    public Task<ResponseData> Restore(Guid id, CancellationToken cancellationToken) =>
        SetSuspended(id, false, null, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<ResponseData> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await mediator.Send(new DeleteDirectoryServerCommand(new DirectoryServerId(id)), cancellationToken))
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    private async Task<ResponseData> Review(Guid id, bool approved, string? message,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReviewDirectoryServerCommand(new DirectoryServerId(id),
            authenticationContext.RequireAdministratorId(), approved, message), cancellationToken);
        if (result == ReviewDirectoryServerResult.NotFound)
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        if (result == ReviewDirectoryServerResult.InvalidState)
        {
            throw new KnownException("server_already_reviewed", StatusCodes.Status409Conflict);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    private async Task<ResponseData> SetSuspended(Guid id, bool suspended, string? reason,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetDirectoryServerSuspendedCommand(new DirectoryServerId(id),
            authenticationContext.RequireAdministratorId(), suspended, reason), cancellationToken);
        if (result == SetDirectoryServerStateResult.NotFound)
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        if (result == SetDirectoryServerStateResult.InvalidState)
        {
            throw new KnownException("server_state_unchanged", StatusCodes.Status409Conflict);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }
}

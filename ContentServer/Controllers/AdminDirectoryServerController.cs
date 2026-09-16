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

    [HttpPut("{id:guid}")]
    public async Task<ResponseData> Update(Guid id, UpdateDirectoryServerRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Validate(request);
        var result = await mediator.Send(new UpdateDirectoryServerCommand(new DirectoryServerId(id), null,
            request.Name.Trim(), normalized.Address, normalized.Description, normalized.Tags), cancellationToken);
        EnsureUpdated(result);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

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

    internal static (string Address, string? Description, string[] Tags) Validate(UpdateDirectoryServerRequest request)
    {
        var tags = request.Tags ?? [];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 ||
            request.Description?.Length > 1024 || tags.Length > 16 ||
            tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 32) ||
            !ServerDirectoryController.IsValidAddress(request.Address))
        {
            throw new KnownException("invalid_server_submission", StatusCodes.Status400BadRequest);
        }

        return (ServerDirectoryController.NormalizeAddress(request.Address),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), tags);
    }

    internal static void EnsureUpdated(UpdateDirectoryServerResult result)
    {
        if (result == UpdateDirectoryServerResult.NotFound)
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        if (result == UpdateDirectoryServerResult.NotOwned)
        {
            throw new KnownException("server_not_owned", StatusCodes.Status403Forbidden);
        }

        if (result == UpdateDirectoryServerResult.AddressConflict)
        {
            throw new KnownException("server_already_submitted", StatusCodes.Status409Conflict);
        }
    }
}

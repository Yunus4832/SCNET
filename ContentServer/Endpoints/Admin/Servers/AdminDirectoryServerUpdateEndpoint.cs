using ContentServer.Application.Commands;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Endpoints.Publisher.Servers;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Servers;

[HttpPut("/api/v1/admin/servers/{id:guid}")]
[AllowAnonymous]
public sealed class AdminDirectoryServerUpdateEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var request = await ReadBodyAsync<UpdateDirectoryServerRequest>(cancellationToken);
        var result = await Update(id, request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Update(Guid id, UpdateDirectoryServerRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Validate(request);
        var result = await mediator.Send(new UpdateDirectoryServerCommand(new DirectoryServerId(id), null,
            request.Name.Trim(), normalized.Address, normalized.Description, normalized.Tags), cancellationToken);
        EnsureUpdated(result);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    internal static (string Address, string? Description, string[] Tags) Validate(UpdateDirectoryServerRequest request)
    {
        var tags = request.Tags ?? [];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 ||
            request.Description?.Length > 1024 || tags.Length > 16 ||
            tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 32) ||
            !EndpointMappings.IsValidAddress(request.Address))
        {
            throw new KnownException("invalid_server_submission", StatusCodes.Status400BadRequest);
        }

        return (EndpointMappings.NormalizeAddress(request.Address),
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

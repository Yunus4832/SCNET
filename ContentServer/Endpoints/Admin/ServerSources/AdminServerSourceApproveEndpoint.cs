using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.ServerSources;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.ServerSources;

[HttpPost("/api/v1/admin/server-sources/{id:guid}/approve")]
[AllowAnonymous]
public sealed class AdminServerSourceApproveEndpoint(
    IMediator mediator,
    IServerSourceInspectionService inspection,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await Approve(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Approve(Guid id, CancellationToken cancellationToken)
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

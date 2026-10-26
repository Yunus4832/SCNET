using ContentServer.Application.Queries;
using ContentServer.Domain.AuthenticationServices;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.AuthenticationServices;

[HttpGet("/api/v1/admin/authentication-services/{id:guid}/audit")]
[AllowAnonymous]
public sealed class AdminAuthenticationServiceAuditEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var page = ReadPagination().ToPageRequest();
        if (page.PageIndex > 100) { throw new KnownException("invalid_pagination", StatusCodes.Status400BadRequest); }

        var items = await mediator.Send(
            new ListAuthenticationServiceAuditQuery(new AuthenticationServiceRegistrationId(Route<Guid>("id")), page),
            cancellationToken
        );
        await Send.ResponseAsync(
            items.Map(item => new AuthenticationServiceAuditResponse(
                    item.Revision,
                    item.PublisherId,
                    item.Action,
                    item.AdministratorId,
                    item.Message,
                    item.OccurredAt,
                    item.Name,
                    item.ServiceUrl,
                    item.Description,
                    item.Status.ToString().ToLowerInvariant()
                )
            ).AsResponseData(),
            StatusCodes.Status200OK,
            cancellationToken
        );
    }
}

public sealed record AuthenticationServiceAuditResponse(
    long Revision,
    string PublisherId,
    string Action,
    string? AdministratorId,
    string? Message,
    DateTimeOffset OccurredAt,
    string Name,
    string ServiceUrl,
    string? Description,
    string Status
);

using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.AuthenticationServices;

[HttpGet("/api/v1/authentication-services")]
[AllowAnonymous]
public sealed class AuthenticationServiceListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var page = ReadPagination().ToPageRequest();
        if (page.PageIndex > 100) { throw new KnownException("invalid_pagination", StatusCodes.Status400BadRequest); }

        var items = await mediator.Send(new ListPublicAuthenticationServicesQuery(page), cancellationToken);
        await Send.ResponseAsync(
            items.Map(item => new AuthenticationServiceResponse(
                    item.Id.ToString(),
                    item.Name,
                    item.ServiceUrl,
                    item.Description,
                    item.ReviewedAt
                )
            ).AsResponseData(),
            StatusCodes.Status200OK,
            cancellationToken
        );
    }
}

public sealed record AuthenticationServiceResponse(
    string Id,
    string Name,
    string ServiceUrl,
    string? Description,
    DateTimeOffset? ReviewedAt
);

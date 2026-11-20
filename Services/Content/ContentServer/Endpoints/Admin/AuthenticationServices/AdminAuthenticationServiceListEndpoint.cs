using ContentServer.Application.Queries;
using ContentServer.Endpoints.AuthenticationServices;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.AuthenticationServices;

[HttpGet("/api/v1/admin/authentication-services")]
[AllowAnonymous]
public sealed class AdminAuthenticationServiceListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var page = ReadPagination().ToPageRequest();
        if (page.PageIndex > 100) { throw new KnownException("invalid_pagination", StatusCodes.Status400BadRequest); }

        var status = AuthenticationDirectoryInput.ParseStatus(Query<string>("status", isRequired: false));
        var items = await mediator.Send(new ListAuthenticationServicesQuery(page, Status: status), cancellationToken);
        await Send.ResponseAsync(
            items.Map(AuthenticationServiceRegistrationResponse.From).AsResponseData(),
            StatusCodes.Status200OK,
            cancellationToken
        );
    }
}

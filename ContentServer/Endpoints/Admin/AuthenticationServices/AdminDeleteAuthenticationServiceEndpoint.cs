using ContentServer.Application.Commands;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Endpoints.AuthenticationServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.AuthenticationServices;

[HttpDelete("/api/v1/admin/authentication-services/{id:guid}")]
[AllowAnonymous]
public sealed class AdminDeleteAuthenticationServiceEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var revision = Query<long>("revision");
        AuthenticationDirectoryInput.RequireRevision(revision);
        var result = await mediator.Send(
            new DeleteAdminAuthenticationServiceCommand(
                new AuthenticationServiceRegistrationId(Route<Guid>("id")),
                revision,
                authenticationContext.RequireAdministratorId(),
                DateTimeOffset.UtcNow
            ),
            cancellationToken
        );
        AuthenticationDirectoryInput.RequireSuccess(result);
        await Send.ResponseAsync(
            new ResponseData(true, "success", StatusCodes.Status200OK),
            StatusCodes.Status200OK,
            cancellationToken
        );
    }
}

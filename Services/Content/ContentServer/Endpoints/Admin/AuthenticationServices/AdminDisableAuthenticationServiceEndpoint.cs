using ContentServer.Application.Commands;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Endpoints.AuthenticationServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.AuthenticationServices;

[HttpPost("/api/v1/admin/authentication-services/{id:guid}/disable")]
[AllowAnonymous]
public sealed class AdminDisableAuthenticationServiceEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<AdminDisableAuthenticationServiceRequest>(cancellationToken);
        var revision = request.Revision;
        AuthenticationDirectoryInput.RequireRevision(revision);
        AuthenticationDirectoryInput.RequireMessage(request.Reason);
        var result = await mediator.Send(
            new DisableAuthenticationServiceCommand(
                new AuthenticationServiceRegistrationId(Route<Guid>("id")),
                revision,
                authenticationContext.RequireAdministratorId(),
                request.Reason,
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

public sealed record AdminDisableAuthenticationServiceRequest(long Revision, string? Reason);

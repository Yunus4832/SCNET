using ContentServer.Application.Commands;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Endpoints.AuthenticationServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.AuthenticationServices;

[HttpPost("/api/v1/admin/authentication-services/{id:guid}/reject")]
[AllowAnonymous]
public sealed class AdminRejectAuthenticationServiceEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<AdminRejectAuthenticationServiceRequest>(cancellationToken);
        AuthenticationDirectoryInput.RequireRevision(request.Revision);
        AuthenticationDirectoryInput.RequireMessage(request.Message);
        var id = new AuthenticationServiceRegistrationId(Route<Guid>("id"));
        var result = await mediator.Send(
            new ReviewAuthenticationServiceCommand(
                id,
                request.Revision,
                authenticationContext.RequireAdministratorId(),
                false,
                request.Message,
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

public sealed record AdminRejectAuthenticationServiceRequest(long Revision, string? Message);

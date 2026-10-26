using ContentServer.Application.Commands;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Endpoints.AuthenticationServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Publisher.AuthenticationServices;

[HttpDelete("/api/v1/publisher/authentication-services/{id:guid}")]
[AllowAnonymous]
public sealed class PublisherDeleteAuthenticationServiceEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var revision = Query<long>("revision");
        AuthenticationDirectoryInput.RequireRevision(revision);
        var result = await mediator.Send(
            new DeletePublisherAuthenticationServiceCommand(
                new AuthenticationServiceRegistrationId(Route<Guid>("id")),
                revision,
                authenticationContext.RequirePublisherId(),
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

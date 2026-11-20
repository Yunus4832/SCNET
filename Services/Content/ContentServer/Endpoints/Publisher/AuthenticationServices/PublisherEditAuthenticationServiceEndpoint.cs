using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Endpoints.AuthenticationServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Endpoints.Publisher.AuthenticationServices;

[HttpPost("/api/v1/publisher/authentication-services/{id:guid}/edit")]
[AllowAnonymous]
public sealed class PublisherEditAuthenticationServiceEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    AuthenticationInspectionClient inspection
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var publisherId = authenticationContext.RequirePublisherId();
        var publisher = await mediator.Send(new GetPublisherQuery(publisherId), cancellationToken);
        if (publisher?.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        var request = await ReadBodyAsync<EditAuthenticationServiceRequest>(cancellationToken);
        var id = new AuthenticationServiceRegistrationId(Route<Guid>("id"));
        AuthenticationDirectoryInput.RequireRevision(request.Revision);
        var owned = await mediator.Send(new GetAuthenticationServiceQuery(id, publisherId), cancellationToken) ??
                    throw new KnownException("authentication_service_not_found", StatusCodes.Status404NotFound);
        if (owned.Revision != request.Revision)
        {
            throw new KnownException("authentication_service_conflict", StatusCodes.Status409Conflict);
        }

        var endpoint = AuthenticationDirectoryInput.Parse(
            request.Name,
            request.ServiceUrl,
            request.Description
        );
        try
        {
            await inspection.InspectAsync(endpoint, cancellationToken);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
                                      error is HttpRequestException or OperationCanceledException
                                          or AuthenticationProtocolException or AuthenticationRemoteException
                                          or System.Text.Json.JsonException
                                          or System.Security.Cryptography.CryptographicException or FormatException)
        {
            throw new KnownException(
                "authentication_service_unavailable_or_invalid",
                StatusCodes.Status422UnprocessableEntity
            );
        }

        try
        {
            AuthenticationDirectoryInput.RequireSuccess(
                await mediator.Send(
                    new EditAuthenticationServiceCommand(
                        id,
                        publisherId,
                        request.Revision,
                        request.Name,
                        endpoint,
                        request.Description,
                        DateTimeOffset.UtcNow
                    ),
                    cancellationToken
                )
            );
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("authentication_service_conflict", StatusCodes.Status409Conflict);
        }

        await Send.ResponseAsync(
            new ResponseData(true, "success", StatusCodes.Status200OK),
            StatusCodes.Status200OK,
            cancellationToken
        );
    }
}

public sealed record EditAuthenticationServiceRequest(
    string Name,
    string ServiceUrl,
    string? Description,
    long Revision
);

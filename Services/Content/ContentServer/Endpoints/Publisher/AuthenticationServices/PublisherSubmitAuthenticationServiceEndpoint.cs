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

[HttpPost("/api/v1/publisher/authentication-services")]
[AllowAnonymous]
public sealed class PublisherSubmitAuthenticationServiceEndpoint(
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

        var request = await ReadBodyAsync<SubmitAuthenticationServiceRequest>(cancellationToken);
        var serviceUrl = AuthenticationDirectoryInput.Parse(request.Name, request.ServiceUrl, request.Description);
        try
        {
            await inspection.InspectAsync(serviceUrl, cancellationToken);
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

        AuthenticationServiceRegistrationId id;
        try
        {
            id = await mediator.Send(
                new SubmitAuthenticationServiceCommand(
                    publisherId,
                    request.Name,
                    serviceUrl,
                    request.Description,
                    DateTimeOffset.UtcNow
                ),
                cancellationToken
            );
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("authentication_service_conflict", StatusCodes.Status409Conflict);
        }

        var submitted = await mediator.Send(new GetAuthenticationServiceQuery(id, publisherId), cancellationToken) ??
                        throw new InvalidOperationException("Submitted listing is missing.");
        await Send.ResponseAsync(
            AuthenticationServiceRegistrationResponse.From(submitted)
                .AsResponseData(code: StatusCodes.Status201Created),
            StatusCodes.Status201Created,
            cancellationToken
        );
    }
}

public sealed record SubmitAuthenticationServiceRequest(
    string Name,
    string ServiceUrl,
    string? Description
);

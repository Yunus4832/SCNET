using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Endpoints.AuthenticationServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Endpoints.Admin.AuthenticationServices;

[HttpPost("/api/v1/admin/authentication-services/{id:guid}/approve")]
[AllowAnonymous]
public sealed class AdminApproveAuthenticationServiceEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    AuthenticationInspectionClient inspection
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<AdminApproveAuthenticationServiceRequest>(cancellationToken);
        AuthenticationDirectoryInput.RequireRevision(request.Revision);
        AuthenticationDirectoryInput.RequireMessage(request.Message);
        var id = new AuthenticationServiceRegistrationId(Route<Guid>("id"));
        var listing = await mediator.Send(new GetAuthenticationServiceQuery(id), cancellationToken) ??
                      throw new KnownException("authentication_service_not_found", StatusCodes.Status404NotFound);
        if (listing.Revision != request.Revision || listing.Status != AuthenticationServiceRegistrationStatus.Pending)
        {
            throw new KnownException("authentication_service_conflict", StatusCodes.Status409Conflict);
        }

        var endpoint = AuthenticationDirectoryInput.Parse(
            listing.Name,
            listing.ServiceUrl,
            listing.Description
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

        var result = await mediator.Send(
            new ReviewAuthenticationServiceCommand(
                id,
                request.Revision,
                authenticationContext.RequireAdministratorId(),
                true,
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

public sealed record AdminApproveAuthenticationServiceRequest(long Revision, string? Message);

using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Endpoints.Public.RendezvousServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.RendezvousServices;

[HttpPost("/api/v1/publisher/rendezvous-services")]
[AllowAnonymous]
public sealed class PublisherSubmitRendezvousServiceEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext, RendezvousInspectionClient inspection) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<SubmitRendezvousServiceRequest>(cancellationToken);
        var result = await SubmitRendezvousService(request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<RendezvousServiceRegistrationResponse>> SubmitRendezvousService(
        SubmitRendezvousServiceRequest request,
        CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 ||
            request.Description?.Length > 1000 ||
            !EndpointMappings.TryParsePublicHttpUrl(request.ControlUrl, out var controlUrl))
        {
            throw new KnownException("invalid_rendezvous_service_submission", StatusCodes.Status400BadRequest);
        }

        controlUrl = new Uri(controlUrl.AbsoluteUri.TrimEnd('/') + "/");
        try
        {
            await inspection.InspectAsync(controlUrl, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                          System.Text.Json.JsonException)
        {
            throw new KnownException("rendezvous_service_unavailable_or_invalid", StatusCodes.Status422UnprocessableEntity);
        }

        SubmitRendezvousServiceResult result;
        try
        {
            result = await mediator.Send(new SubmitRendezvousServiceCommand(publisher.PublisherId, request.Name, controlUrl,
                request.Description), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("rendezvous_service_already_submitted", StatusCodes.Status409Conflict);
        }

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        return new RendezvousServiceRegistrationResponse(result.Id.ToString(), publisher.PublisherId.ToString(),
            publisher.DisplayName, request.Name.Trim(), controlUrl.AbsoluteUri, Normalize(request.Description),
            result.Status.ToString().ToLowerInvariant(), null, result.CreatedAt, null)
            .AsResponseData(code: StatusCodes.Status201Created);
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new GetPublisherQuery(authenticationContext.RequirePublisherId()),
            cancellationToken
        ) ?? throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SubmitRendezvousServiceRequest(string Name, string ControlUrl, string? Description);

using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Endpoints.Public.ServerSources;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.ServerSources;

[HttpPost("/api/v1/publisher/server-sources")]
[AllowAnonymous]
public sealed class PublisherSubmitServerSourceEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext, IServerSourceInspectionService serverSourceInspection) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<SubmitServerSourceRequest>(cancellationToken);
        var result = await SubmitServerSource(request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ServerSourceRegistrationResponse>> SubmitServerSource(
        SubmitServerSourceRequest request,
        CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 ||
            request.Description?.Length > 1000 ||
            !EndpointMappings.TryParsePublicHttpUrl(request.ApiUrl, out var apiUrl))
        {
            throw new KnownException("invalid_server_source_submission", StatusCodes.Status400BadRequest);
        }

        try
        {
            await serverSourceInspection.InspectAsync(apiUrl, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                          ServerSource.Protocol.ServerSourceProtocolException)
        {
            throw new KnownException("server_source_unavailable_or_invalid", StatusCodes.Status422UnprocessableEntity);
        }

        SubmitServerSourceResult result;
        try
        {
            result = await mediator.Send(new SubmitServerSourceCommand(publisher.PublisherId, request.Name, apiUrl,
                request.Description), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("server_source_already_submitted", StatusCodes.Status409Conflict);
        }

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        return new ServerSourceRegistrationResponse(result.Id.ToString(), publisher.PublisherId.ToString(),
            publisher.DisplayName, request.Name.Trim(), apiUrl.AbsoluteUri, Normalize(request.Description),
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

public sealed record SubmitServerSourceRequest(string Name, string ApiUrl, string? Description);

using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Servers;

[HttpPost("/api/v1/publisher/servers")]
[AllowAnonymous]
public sealed class PublisherSubmitServerEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<SubmitDirectoryServerRequest>(cancellationToken);
        var result = await SubmitServer(request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<DirectoryServerResponse>> SubmitServer(SubmitDirectoryServerRequest request,
        CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        var tags = request.Tags ?? [];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 ||
            request.Description?.Length > 1024 || tags.Length > 16 ||
            tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 32) ||
            !EndpointMappings.IsValidAddress(request.Address))
        {
            throw new KnownException("invalid_server_submission", StatusCodes.Status400BadRequest);
        }

        DirectoryServerId id;
        try
        {
            id = await mediator.Send(new SubmitDirectoryServerCommand(publisher.PublisherId, request.Name.Trim(),
                    EndpointMappings.NormalizeAddress(request.Address), Normalize(request.Description), tags),
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("server_already_submitted", StatusCodes.Status409Conflict);
        }

        var server = (await mediator.Send(new ListDirectoryServersQuery(Id: id), cancellationToken)).Single();
        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        return EndpointMappings.DirectoryServer(server).AsResponseData(code: StatusCodes.Status201Created);
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

public sealed record SubmitDirectoryServerRequest(string Name, string Address, string? Description, string[] Tags);

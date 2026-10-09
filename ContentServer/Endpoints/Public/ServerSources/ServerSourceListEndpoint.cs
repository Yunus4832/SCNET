using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Public.ServerSources;

[HttpGet("/api/v1/server-sources")]
[AllowAnonymous]
public sealed class ServerSourceListEndpoint(IMediator mediator, IOptions<ContentServerOptions> options)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await List(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ServerSourceResponse[]>> List(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new ListServerSourcesQuery(PublicOnly: true), cancellationToken);
        var sources = items.OrderBy(source => source.Name)
            .Select(source => new ServerSourceResponse(source.Id.ToString(), source.Name, source.ApiUrl,
                source.Description)).ToList();
        if (options.Value.BuiltInServerDirectoryEnabled)
        {
            sources.Insert(0, new ServerSourceResponse("builtin", options.Value.BuiltInServerDirectoryName,
                GetBuiltInDirectoryUrl(options.Value, HttpContext.Request),
                options.Value.BuiltInServerDirectoryDescription));
        }

        return sources.ToArray().AsResponseData();
    }

    internal static string GetBuiltInDirectoryUrl(ContentServerOptions options, HttpRequest request)
    {
        var baseUrl = options.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}/";
        }

        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "api/v1/server-directory").AbsoluteUri;
    }
}

public sealed record ServerSourceResponse(string Id, string Name, string ApiUrl, string? Description);

public sealed record ServerSourceRegistrationResponse(
    string Id,
    string PublisherId,
    string PublisherName,
    string Name,
    string ApiUrl,
    string? Description,
    string Status,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record ServerSourceManagementResponse(
    string Id,
    string? PublisherId,
    string? PublisherName,
    string Name,
    string ApiUrl,
    string? Description,
    string Status,
    string? ReviewMessage,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ReviewedAt,
    bool IsBuiltIn);

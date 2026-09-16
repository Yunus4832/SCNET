using ContentServer.Application;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Responses;

using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ServerSourceController(IMediator mediator, IOptions<ContentServerOptions> options) : ControllerBase
{
    [HttpGet("server-sources")]
    public async Task<ResponseData<ServerSourceResponse[]>> List(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new ListServerSourcesQuery(PublicOnly: true), cancellationToken);
        var sources = items.OrderBy(source => source.Name)
            .Select(source => new ServerSourceResponse(source.Id.ToString(), source.Name, source.ApiUrl,
                source.Description)).ToList();
        if (options.Value.BuiltInServerDirectoryEnabled)
        {
            sources.Insert(0, new ServerSourceResponse("builtin", options.Value.BuiltInServerDirectoryName,
                GetBuiltInDirectoryUrl(options.Value, Request), options.Value.BuiltInServerDirectoryDescription));
        }

        return sources.ToArray().AsResponseData();
    }

    internal static bool TryParsePublicHttpUrl(string value, out Uri uri)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out uri!) &&
               uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Fragment);
    }

    internal static ServerSourceRegistrationResponse Map(ServerSourceDto source)
    {
        return new ServerSourceRegistrationResponse(source.Id.ToString(), source.PublisherId.ToString(),
            source.PublisherName, source.Name, source.ApiUrl, source.Description,
            source.Status.ToString().ToLowerInvariant(), source.ReviewMessage, source.CreatedAt, source.ReviewedAt);
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

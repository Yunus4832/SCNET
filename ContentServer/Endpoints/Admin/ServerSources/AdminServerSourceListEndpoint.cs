using ContentServer.Application.Queries;
using ContentServer.Domain.ServerSources;
using ContentServer.Endpoints.Public.ServerSources;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.ServerSources;

[HttpGet("/api/v1/admin/server-sources")]
[AllowAnonymous]
public sealed class AdminServerSourceListEndpoint(IMediator mediator, IOptions<ContentServerOptions> options)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var status = Query<ServerSourceRegistrationStatus?>("status", isRequired: false);
        var result = await List(status, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ServerSourceManagementResponse[]>> List(
        ServerSourceRegistrationStatus? status, CancellationToken cancellationToken)
    {
        var sources = await mediator.Send(new ListServerSourcesQuery(status), cancellationToken);
        var responses = sources.Select(source => new ServerSourceManagementResponse(source.Id.ToString(),
            source.PublisherId.ToString(), source.PublisherName, source.Name, source.ApiUrl, source.Description,
            source.Status.ToString().ToLowerInvariant(), source.ReviewMessage, source.CreatedAt, source.ReviewedAt,
            false)).ToList();
        if (status is null && options.Value.BuiltInServerDirectoryEnabled)
        {
            responses.Insert(0, new ServerSourceManagementResponse("builtin", null, null,
                options.Value.BuiltInServerDirectoryName,
                EndpointMappings.GetBuiltInDirectoryUrl(options.Value, HttpContext.Request),
                options.Value.BuiltInServerDirectoryDescription, "active", null, null, null, true));
        }

        return responses.ToArray().AsResponseData();
    }
}

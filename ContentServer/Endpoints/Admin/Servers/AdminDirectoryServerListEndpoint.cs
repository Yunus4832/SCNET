using ContentServer.Application.Queries;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Endpoints.Publisher.Servers;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Servers;

[HttpGet("/api/v1/admin/servers")]
[AllowAnonymous]
public sealed class AdminDirectoryServerListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var status = Query<DirectoryServerReviewStatus?>("status", isRequired: false);
        var result = await List(status, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<DirectoryServerResponse[]>> List(
        DirectoryServerReviewStatus? status, CancellationToken cancellationToken)
    {
        var servers = await mediator.Send(new ListDirectoryServersQuery(ReviewStatus: status), cancellationToken);
        return servers.Select(EndpointMappings.DirectoryServer).ToArray().AsResponseData();
    }
}

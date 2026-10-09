using ContentServer.Application.Queries;
using ContentServer.Endpoints.Public.Contents;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Publisher.Contents;

[HttpGet("/api/v1/publisher/submissions")]
[AllowAnonymous]
public sealed class PublisherSubmissionsEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var query = Query<string?>("query", isRequired: false);
        var page = ReadPagination();
        var result = await Submissions(query, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ContentVersionResponse>>> Submissions(
        string? query,
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var publisherId = authenticationContext.RequirePublisherId();
        var items = await mediator.Send(
            new ListVersionsQuery(
                VersionQueryScope.Publisher,
                page.ToPageRequest(),
                Search: query,
                PublisherId: publisherId),
            cancellationToken
        );
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }
}

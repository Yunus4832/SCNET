using ContentServer.Application.Queries;
using ContentServer.Domain.Contents;
using ContentServer.Endpoints.Public.Contents;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Contents;

[HttpGet("/api/v1/admin/submissions")]
[AllowAnonymous]
public sealed class AdminSubmissionsEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var status = Query<ContentVersionStatus?>("status", isRequired: false);
        var query = Query<string?>("query", isRequired: false);
        var page = ReadPagination();
        var result = await Submissions(status, query, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ContentVersionResponse>>> Submissions(
        ContentVersionStatus? status,
        string? query,
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(VersionQueryScope.All, page.ToPageRequest(), Search: query, Status: status),
            cancellationToken);
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }
}

using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Endpoints.Public.Contents;
using ContentServer.Endpoints.Publisher.Account;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Publishers;

[HttpGet("/api/v1/admin/publishers")]
[AllowAnonymous]
public sealed class AdminPublishersEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var status = Query<PublisherStatus?>("status", isRequired: false);
        var query = Query<string?>("query", isRequired: false);
        var page = ReadPagination();
        var result = await Publishers(status, query, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<PublisherResponse>>> Publishers(
        PublisherStatus? status,
        string? query,
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListPublishersQuery(page.ToPageRequest(), status, query),
            cancellationToken);
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }
}

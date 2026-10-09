using ContentServer.Application.Queries;
using ContentServer.Domain.Administration;
using ContentServer.Endpoints.Public.Contents;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpGet("/api/v1/admin/administrator-applications")]
[AllowAnonymous]
public sealed class AdminAdministratorApplicationsEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var status = Query<AdministratorStatus?>("status", isRequired: false);
        var query = Query<string?>("query", isRequired: false);
        var page = ReadPagination();
        var result = await AdministratorApplications(status, query, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<AdministratorResponse>>> AdministratorApplications(
        AdministratorStatus? status, string? query,
        PaginationRequest page, CancellationToken ct)
    {
        var items = await mediator.Send(new ListAdministratorsQuery(page.ToPageRequest(), status, Search: query), ct);
        return items.Map(EndpointMappings.Administrator).AsResponseData();
    }
}

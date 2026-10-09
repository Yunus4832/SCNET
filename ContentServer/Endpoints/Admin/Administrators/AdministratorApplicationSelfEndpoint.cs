using ContentServer.Application.Queries;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpGet("/api/v1/administrator")]
[AllowAnonymous]
public sealed class AdministratorApplicationSelfEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await Self(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<AdministratorResponse>> Self(CancellationToken ct)
    {
        var id = authenticationContext.RequireAdministratorId();
        var page = await mediator.Send(
            new ListAdministratorsQuery(new PageRequest { PageIndex = 1, PageSize = 1, CountTotal = false },
                AdministratorId: id), ct);
        var item = page.Items.FirstOrDefault(x => x.AdministratorId == id) ??
                   throw new KnownException("administrator_not_found", 404);
        return ToResponse(item).AsResponseData();
    }

    internal static AdministratorResponse ToResponse(AdministratorDto x) => new(x.AdministratorId.ToString(), x.Name,
        x.Contact, x.Description, x.Status.ToString().ToLowerInvariant(), x.IsSuperAdministrator, x.HasActiveKey,
        x.ReviewMessage, x.CreatedAt, x.ReviewedAt);
}

public sealed record AdministratorResponse(
    string AdministratorId,
    string Name,
    string Contact,
    string? Description,
    string Status,
    bool IsSuperAdministrator,
    bool HasActiveKey,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record AdministratorApplicationResponse(string AdministratorId, string Status, string ApiKey);

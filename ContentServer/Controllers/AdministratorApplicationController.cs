using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;
using ContentServer.Middlewares;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AdministratorApplicationController(IMediator mediator, ApiKeyAuthenticationContext authenticationContext) : ControllerBase
{
    [HttpPost("administrators/applications")]
    public async Task<ResponseData<AdministratorApplicationResponse>> Apply(ApplyAdministratorRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Contact))
        {
            throw new KnownException("invalid_application", 400);
        }

        var r = await mediator.Send(new ApplyAdministratorCommand(request.Name, request.Contact, request.Description), ct);
        Response.StatusCode = 201; return new AdministratorApplicationResponse(r.AdministratorId.ToString(), r.Status.ToString().ToLowerInvariant(), r.ApiKey).AsResponseData(code: 201);
    }

    [HttpGet("administrator")]
    public async Task<ResponseData<AdministratorResponse>> Self(CancellationToken ct)
    {
        var id = authenticationContext.RequireAdministratorId();
        var page = await mediator.Send(new ListAdministratorsQuery(new PageRequest { PageIndex = 1, PageSize = 1, CountTotal = false }, AdministratorId: id), ct);
        var item = page.Items.FirstOrDefault(x => x.AdministratorId == id) ?? throw new KnownException("administrator_not_found", 404);
        return ToResponse(item).AsResponseData();
    }

    internal static AdministratorResponse ToResponse(AdministratorDto x) => new(x.AdministratorId.ToString(), x.Name, x.Contact, x.Description, x.Status.ToString().ToLowerInvariant(), x.IsSuperAdministrator, x.HasActiveKey, x.ReviewMessage, x.CreatedAt, x.ReviewedAt);
}

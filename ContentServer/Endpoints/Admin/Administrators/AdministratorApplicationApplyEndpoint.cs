using ContentServer.Application.Commands;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpPost("/api/v1/administrators/applications")]
[AllowAnonymous]
public sealed class AdministratorApplicationApplyEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<ApplyAdministratorRequest>(cancellationToken);
        var result = await Apply(request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<AdministratorApplicationResponse>> Apply(ApplyAdministratorRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Contact))
        {
            throw new KnownException("invalid_application", 400);
        }

        var r = await mediator.Send(new ApplyAdministratorCommand(request.Name, request.Contact, request.Description),
            ct);
        HttpContext.Response.StatusCode = 201;
        return new AdministratorApplicationResponse(r.AdministratorId.ToString(),
            r.Status.ToString().ToLowerInvariant(), r.ApiKey).AsResponseData(code: 201);
    }
}

public sealed record ApplyAdministratorRequest(string Name, string Contact, string? Description);

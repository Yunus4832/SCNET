using ContentServer.Middlewares;

using FastEndpoints;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpGet("/api/v1/admin/self")]
[AllowAnonymous]
public sealed class AdminSelfEndpoint(ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = Self();
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private ResponseData<AdministratorSelfResponse> Self()
    {
        return new AdministratorSelfResponse(
            authenticationContext.RequireAdministratorId().ToString(),
            "active").AsResponseData();
    }
}

public sealed record AdministratorSelfResponse(string AdministratorId, string Status);

using FastEndpoints;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Public.Health;

[HttpGet("/api/v1/health")]
[AllowAnonymous]
public sealed class PublicHealthEndpoint : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = Health();
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private ResponseData<HealthResponse> Health()
    {
        return new HealthResponse("ContentServer", "v1").AsResponseData();
    }
}

public sealed record HealthResponse(string Name, string Version);

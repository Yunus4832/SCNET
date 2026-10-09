using ContentServer.Application.Queries;
using ContentServer.Utils;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpGet("/api/v1/administrators/initialization")]
[AllowAnonymous]
public sealed class AdministratorInitializationInitializationStatusEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await InitializationStatus(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<AdministratorInitializationStatusResponse>> InitializationStatus(
        CancellationToken cancellationToken)
    {
        var required = await mediator.Send(
            new GetAdministratorInitializationQuery(),
            cancellationToken);
        return new AdministratorInitializationStatusResponse(
            required,
            ApiKeyUtility.MinimumLength,
            ApiKeyUtility.MaximumLength,
            ApiKeyUtility.AllowedCharacters).AsResponseData();
    }
}

public sealed record AdministratorInitializationStatusResponse(
    bool Required,
    int ApiKeyMinimumLength,
    int ApiKeyMaximumLength,
    string ApiKeyAllowedCharacters);

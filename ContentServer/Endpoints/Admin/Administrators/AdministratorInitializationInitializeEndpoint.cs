using ContentServer.Application.Commands;
using ContentServer.Utils;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpPost("/api/v1/administrators/initialize")]
[AllowAnonymous]
public sealed class AdministratorInitializationInitializeEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<InitializeAdministratorRequest>(cancellationToken);
        var result = await Initialize(request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<AdministratorInitializationResponse>> Initialize(
        InitializeAdministratorRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new KnownException("invalid_administrator", StatusCodes.Status400BadRequest);
        }

        if (!ApiKeyUtility.IsValid(request.ApiKey))
        {
            throw new KnownException("invalid_api_key", StatusCodes.Status400BadRequest);
        }

        var result = await mediator.Send(
            new InitializeAdministratorCommand(request.Name.Trim(), request.ApiKey),
            cancellationToken
        ) ?? throw new KnownException(
            "administrator_already_initialized",
            StatusCodes.Status409Conflict);

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        return new AdministratorInitializationResponse(
            result.AdministratorId.ToString(),
            result.Name,
            result.Status.ToString().ToLowerInvariant()
        ).AsResponseData(code: StatusCodes.Status201Created);
    }
}

public sealed record AdministratorInitializationResponse(
    string AdministratorId,
    string Name,
    string Status);

public sealed record InitializeAdministratorRequest(
    string Name,
    string ApiKey);

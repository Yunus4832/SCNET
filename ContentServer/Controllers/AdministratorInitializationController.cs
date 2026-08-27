using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;
using ContentServer.Utils;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1/administrators")]
public sealed class AdministratorInitializationController(
    IMediator mediator
) : ControllerBase
{
    [HttpGet("initialization")]
    public async Task<ResponseData<AdministratorInitializationStatusResponse>> InitializationStatus(
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

    [HttpPost("initialize")]
    public async Task<ResponseData<AdministratorInitializationResponse>> Initialize(
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

        Response.StatusCode = StatusCodes.Status201Created;
        return new AdministratorInitializationResponse(
            result.AdministratorId.ToString(),
            result.Name,
            result.Status.ToString().ToLowerInvariant()
        ).AsResponseData(code: StatusCodes.Status201Created);
    }
}

using ContentServer.Application.Commands;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Publishers;

[HttpPost("/api/v1/publishers")]
[AllowAnonymous]
public sealed class PublisherApplicationApplyEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<CreatePublisherRequest>(cancellationToken);
        var result = await Apply(request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PublisherApplicationResponse>> Apply(
        CreatePublisherRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Contact))
        {
            throw new KnownException("invalid_application", StatusCodes.Status400BadRequest);
        }

        var result = await mediator.Send(
            new ApplyPublisherCommand(
                request.DisplayName.Trim(),
                request.Contact.Trim(),
                request.Description),
            cancellationToken
        );

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        return new PublisherApplicationResponse(
            result.PublisherId.ToString(),
            result.Status.ToString().ToLowerInvariant(),
            result.ApiKey).AsResponseData(code: StatusCodes.Status201Created);
    }
}

public sealed record PublisherApplicationResponse(
    string PublisherId,
    string Status,
    string ApiKey);

public sealed record CreatePublisherRequest(
    string DisplayName,
    string Contact,
    string? Description);

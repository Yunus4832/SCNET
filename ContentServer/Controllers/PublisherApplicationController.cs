using ContentServer.Application.Commands;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1/publishers")]
public sealed class PublisherApplicationController(
    IMediator mediator
) : ControllerBase
{
    [HttpPost]
    public async Task<ResponseData<PublisherApplicationResponse>> Apply(
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

        Response.StatusCode = StatusCodes.Status201Created;
        return new PublisherApplicationResponse(
            result.PublisherId.ToString(),
            result.Status.ToString().ToLowerInvariant(),
            result.ApiKey).AsResponseData(code: StatusCodes.Status201Created);
    }
}

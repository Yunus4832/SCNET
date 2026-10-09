using System.Text.Json;

using ContentServer.Endpoints.Public.Contents;

using FastEndpoints;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints;

public abstract class ContentEndpoint : EndpointWithoutRequest
{
    protected PaginationRequest ReadPagination()
    {
        return new PaginationRequest
        {
            PageIndex = Query<int>("pageIndex", isRequired: false),
            PageSize = Query<int>("pageSize", isRequired: false)
        };
    }

    protected async Task<T> ReadBodyAsync<T>(CancellationToken cancellationToken)
    {
        try
        {
            return await HttpContext.Request.ReadFromJsonAsync<T>(cancellationToken)
                   ?? throw new KnownException("invalid_request", StatusCodes.Status400BadRequest);
        }
        catch (JsonException)
        {
            throw new KnownException("invalid_request", StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("invalid_request", StatusCodes.Status400BadRequest);
        }
    }
}

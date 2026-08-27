using NetCorePal.Extensions.Dto;

namespace ContentServer.Controllers.Contracts.Requests;

public sealed class PaginationRequest
{
    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public PageRequest ToPageRequest()
    {
        return new PageRequest
        {
            PageIndex = PageIndex > 0 ? PageIndex : 1,
            PageSize = Math.Clamp(PageSize > 0 ? PageSize : 10, 1, 100),
            CountTotal = true
        };
    }
}

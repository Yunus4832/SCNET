using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Responses;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Controllers.Mappings;

internal static class ResponseMappingExtensions
{
    public static PublisherResponse ToResponse(this PublisherDto publisher)
    {
        return new PublisherResponse(
            publisher.PublisherId.ToString(),
            publisher.DisplayName,
            publisher.Contact,
            publisher.Description,
            publisher.Status.ToString().ToLowerInvariant(),
            publisher.HasActiveKey,
            publisher.ReviewMessage,
            publisher.CreatedAt,
            publisher.ReviewedAt);
    }

    public static ContentVersionResponse ToResponse(this ContentVersionDto version)
    {
        return new ContentVersionResponse(
            version.ContentId,
            version.PublisherId,
            version.Type,
            version.Identifier,
            version.Name,
            version.Summary,
            version.ContentStatus.ToString().ToLowerInvariant(),
            version.VersionId,
            version.Version,
            version.PackageHash,
            version.PackageSize,
            version.FileName,
            version.MetadataJson,
            version.Status.ToString().ToLowerInvariant(),
            version.ReviewMessage,
            version.CreatedAt,
            version.PublishedAt,
            $"/api/v1/packages/{version.PackageHash}");
    }

    public static ModPackageResponse ToModResponse(this ContentVersionDto version)
    {
        return new ModPackageResponse(
            version.Identifier,
            version.Version,
            version.PackageHash,
            version.FileName,
            version.PackageSize,
            "Both",
            version.Summary,
            version.CreatedAt,
            $"/api/v1/packages/{version.PackageHash}");
    }

    public static PagedData<TResult> Map<TSource, TResult>(
        this PagedData<TSource> page,
        Func<TSource, TResult> map)
    {
        return new PagedData<TResult>(
            page.Items.Select(map),
            page.Total,
            page.PageIndex,
            page.PageSize);
    }
}

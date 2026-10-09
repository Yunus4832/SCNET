using System.Text.Json;

using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Endpoints.Admin.Administrators;
using ContentServer.Endpoints.Public.ServerSources;
using ContentServer.Endpoints.Publisher.Servers;

using NetCorePal.Extensions.Primitives;

using ServerSource.Protocol;

namespace ContentServer.Endpoints;

internal static class EndpointMappings
{
    public static AdministratorResponse Administrator(AdministratorDto item) =>
        new(item.AdministratorId.ToString(), item.Name, item.Contact, item.Description,
            item.Status.ToString().ToLowerInvariant(), item.IsSuperAdministrator, item.HasActiveKey,
            item.ReviewMessage, item.CreatedAt, item.ReviewedAt);

    public static DirectoryServerResponse DirectoryServer(DirectoryServerDto server) =>
        new(server.Id.ToString(), server.PublisherId.ToString(), server.PublisherName,
            server.Name, server.Address, server.Description,
            JsonSerializer.Deserialize<string[]>(server.TagsJson) ?? [],
            server.ReviewStatus.ToString().ToLowerInvariant(), server.ReviewMessage, server.IsEnabledByPublisher,
            server.SuspendedAt is not null, server.SuspensionReason, server.CreatedAt, server.UpdatedAt,
            server.ReviewedAt);

    public static ServerSourceRegistrationResponse ServerSource(ServerSourceDto source) =>
        new(source.Id.ToString(), source.PublisherId.ToString(), source.PublisherName,
            source.Name, source.ApiUrl, source.Description,
            source.Status.ToString().ToLowerInvariant(), source.ReviewMessage, source.CreatedAt, source.ReviewedAt);

    public static bool IsValidAddress(string value) => ServerSourceValidator.IsValidServerAddress(value);

    public static string NormalizeAddress(string value)
    {
        var uri = new Uri($"tcp://{value.Trim()}");
        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{uri.Host.Trim('[', ']')}]"
            : uri.IdnHost.ToLowerInvariant();
        return $"{host}:{uri.Port}";
    }

    public static bool TryParsePublicHttpUrl(string value, out Uri uri) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out uri!) &&
        uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Fragment);

    public static string GetBuiltInDirectoryUrl(ContentServerOptions options, HttpRequest request)
    {
        var baseUrl = options.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}/";
        }

        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "api/v1/server-directory").AbsoluteUri;
    }

    public static (string Address, string? Description, string[] Tags) ValidateServerUpdate(
        UpdateDirectoryServerRequest request)
    {
        var tags = request.Tags ?? [];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 ||
            request.Description?.Length > 1024 || tags.Length > 16 ||
            tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 32) ||
            !IsValidAddress(request.Address))
        {
            throw new KnownException("invalid_server_submission", StatusCodes.Status400BadRequest);
        }

        return (NormalizeAddress(request.Address),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), tags);
    }

    public static void EnsureServerUpdated(UpdateDirectoryServerResult result)
    {
        if (result == UpdateDirectoryServerResult.NotFound)
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        if (result == UpdateDirectoryServerResult.NotOwned)
        {
            throw new KnownException("server_not_owned", StatusCodes.Status403Forbidden);
        }

        if (result == UpdateDirectoryServerResult.AddressConflict)
        {
            throw new KnownException("server_already_submitted", StatusCodes.Status409Conflict);
        }
    }
}

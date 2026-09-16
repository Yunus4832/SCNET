using System.Buffers.Binary;
using System.Text.Json;

using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Responses;

using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using ServerSource.Protocol;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1/server-directory")]
public sealed class ServerDirectoryController(IMediator mediator, IOptions<ContentServerOptions> options)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ServerSourcePage>> List([FromQuery] int limit = 100,
        [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        if (!options.Value.BuiltInServerDirectoryEnabled)
        {
            return NotFound();
        }

        if (limit is < 1 or > 100 || !TryDecodeCursor(cursor, out var offset))
        {
            return BadRequest();
        }

        var items = await mediator.Send(new ListDirectoryServersQuery(PublicOnly: true, Offset: offset,
            Limit: limit + 1), cancellationToken);
        var hasMore = items.Count > limit;
        var servers = items.Take(limit).Select(item => new ServerSourceEntry(item.Id.ToString(), item.Name,
            item.Address, item.Description, JsonSerializer.Deserialize<string[]>(item.TagsJson) ?? [])).ToArray();
        return new ServerSourcePage(ServerSourceProtocol.CurrentVersion,
            new ServerSourceDescriptor(options.Value.BuiltInServerDirectoryId,
                options.Value.BuiltInServerDirectoryName), servers,
            hasMore ? EncodeCursor(offset + limit) : null);
    }

    private static bool TryDecodeCursor(string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrEmpty(cursor))
        {
            return true;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            if (bytes.Length != sizeof(int))
            {
                return false;
            }

            offset = BinaryPrimitives.ReadInt32BigEndian(bytes);
            return offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeCursor(int offset)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, offset);
        return Convert.ToBase64String(bytes);
    }

    internal static bool IsValidAddress(string value)
    {
        return ServerSourceValidator.IsValidServerAddress(value);
    }

    internal static string NormalizeAddress(string value)
    {
        var uri = new Uri($"tcp://{value.Trim()}");
        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{uri.Host.Trim('[', ']')}]"
            : uri.IdnHost.ToLowerInvariant();
        return $"{host}:{uri.Port}";
    }

    internal static DirectoryServerResponse Map(DirectoryServerDto server)
    {
        return new DirectoryServerResponse(server.Id.ToString(), server.PublisherId.ToString(), server.PublisherName,
            server.Name, server.Address, server.Description,
            JsonSerializer.Deserialize<string[]>(server.TagsJson) ?? [],
            server.ReviewStatus.ToString().ToLowerInvariant(), server.ReviewMessage, server.IsEnabledByPublisher,
            server.SuspendedAt is not null, server.SuspensionReason, server.CreatedAt, server.UpdatedAt,
            server.ReviewedAt);
    }
}

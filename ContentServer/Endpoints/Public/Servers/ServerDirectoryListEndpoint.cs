using System.Buffers.Binary;
using System.Text.Json;

using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using ServerSource.Protocol;

namespace ContentServer.Endpoints.Public.Servers;

[HttpGet("/api/v1/server-directory")]
[AllowAnonymous]
public sealed class ServerDirectoryListEndpoint(IMediator mediator, IOptions<ContentServerOptions> options)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var limit = Query<int?>("limit", isRequired: false) ?? 100;
        var cursor = Query<string?>("cursor", isRequired: false);
        if (!options.Value.BuiltInServerDirectoryEnabled)
        {
            await Results.NotFound().ExecuteAsync(HttpContext);
            return;
        }

        if (limit is < 1 or > 100 || !TryDecodeCursor(cursor, out var offset))
        {
            await Results.BadRequest().ExecuteAsync(HttpContext);
            return;
        }

        var items = await mediator.Send(new ListDirectoryServersQuery(PublicOnly: true, Offset: offset,
            Limit: limit + 1), cancellationToken);
        var hasMore = items.Count > limit;
        var servers = items.Take(limit).Select(item => new ServerSourceEntry(item.Id.ToString(), item.Name,
            item.Address, item.Description, JsonSerializer.Deserialize<string[]>(item.TagsJson) ?? [])).ToArray();
        var page = new ServerSourcePage(ServerSourceProtocol.CurrentVersion,
            new ServerSourceDescriptor(options.Value.BuiltInServerDirectoryId,
                options.Value.BuiltInServerDirectoryName), servers,
            hasMore ? EncodeCursor(offset + limit) : null);
        await Send.ResponseAsync(page, cancellation: cancellationToken);
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
}

using System.Net.Http.Headers;
using System.Text.Json;

namespace ServerSource.Protocol;

public sealed class ServerSourceProtocolClient
{
    private const int _maximumPages = 100;
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public ServerSourceProtocolClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ServerSourcePage> GetPageAsync(Uri sourceUrl, string? cursor = null,
        int limit = ServerSourceProtocol.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);
        if (!sourceUrl.IsAbsoluteUri || sourceUrl.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Server source URL must be an absolute HTTP or HTTPS URL.", nameof(sourceUrl));
        }

        if (limit is < 1 or > ServerSourceProtocol.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        if (cursor is { Length: > ServerSourceProtocol.MaximumCursorLength })
        {
            throw new ArgumentException("Cursor is too long.", nameof(cursor));
        }

        var requestUri = BuildRequestUri(sourceUrl, cursor, limit);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        EnsureJsonContentType(response.Content.Headers.ContentType);
        EnsureContentLength(response.Content.Headers.ContentLength);

        var data = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
        ServerSourcePage? page;
        try
        {
            page = JsonSerializer.Deserialize<ServerSourcePage>(data, _serializerOptions);
        }
        catch (JsonException exception)
        {
            throw new ServerSourceProtocolException("Server source response is not valid JSON.", exception);
        }

        var validation = ServerSourceValidator.Validate(page);
        if (!validation.IsValid)
        {
            throw new ServerSourceProtocolException("Server source response failed protocol validation.")
            {
                ValidationIssues = validation.Issues
            };
        }

        return page!;
    }

    public async Task<ServerSourceSnapshot> GetAllAsync(Uri sourceUrl,
        CancellationToken cancellationToken = default)
    {
        var servers = new List<ServerSourceEntry>();
        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        ServerSourceDescriptor? descriptor = null;
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < _maximumPages; pageNumber++)
        {
            var page = await GetPageAsync(sourceUrl, cursor, ServerSourceProtocol.MaximumPageSize,
                cancellationToken).ConfigureAwait(false);
            descriptor ??= page.Source;
            if (!string.Equals(descriptor.Id, page.Source.Id, StringComparison.Ordinal))
            {
                throw new ServerSourceProtocolException("Server source id changed between pages.");
            }

            foreach (var entry in page.Servers)
            {
                if (!entryIds.Add(entry.Id))
                {
                    throw new ServerSourceProtocolException("Server entry id is duplicated between pages.");
                }

                servers.Add(entry);
            }

            cursor = page.NextCursor;
            if (cursor is null)
            {
                return new ServerSourceSnapshot(descriptor, servers);
            }

            if (!cursors.Add(cursor))
            {
                throw new ServerSourceProtocolException("Server source returned a repeated cursor.");
            }
        }

        throw new ServerSourceProtocolException("Server source returned too many pages.");
    }

    private static Uri BuildRequestUri(Uri sourceUrl, string? cursor, int limit)
    {
        var builder = new UriBuilder(sourceUrl);
        var query = builder.Query.TrimStart('?');
        var protocolQuery = $"limit={limit}";
        if (!string.IsNullOrEmpty(cursor))
        {
            protocolQuery += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        builder.Query = string.IsNullOrEmpty(query) ? protocolQuery : $"{query}&{protocolQuery}";
        return builder.Uri;
    }

    private static void EnsureJsonContentType(MediaTypeHeaderValue? contentType)
    {
        var mediaType = contentType?.MediaType;
        if (mediaType is null ||
            !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ServerSourceProtocolException("Server source response must use a JSON content type.");
        }
    }

    private static void EnsureContentLength(long? contentLength)
    {
        if (contentLength > ServerSourceProtocol.MaximumResponseBytes)
        {
            throw new ServerSourceProtocolException("Server source response is too large.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var target = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return target.ToArray();
            }

            if (target.Length + read > ServerSourceProtocol.MaximumResponseBytes)
            {
                throw new ServerSourceProtocolException("Server source response is too large.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}

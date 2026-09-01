using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.Commands;

/// <summary>
///     Loopback HTTP frontend for commands with an explicit HTTP binding.
/// </summary>
public sealed class HttpCommandHost : IDisposable
{
    private const int _maxBodyBytes = 64 * 1024;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpListener _listener = new();
    private readonly string _token;
    private readonly CommandPrincipal _principal;
    private readonly CancellationTokenSource _stopping = new();

    public int Port { get; }

    private HttpCommandHost(int port, string token, CommandPrincipal principal)
    {
        Port = port;
        _token = token;
        _principal = principal;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public static HttpCommandHost Start(int port, string accessToken, CommandPrincipal principal)
    {
        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var host = new HttpCommandHost(
            port,
            ValidateAccessToken(accessToken),
            principal);
        host._listener.Start();
        _ = host.AcceptLoopAsync(host._stopping.Token);
        Log.Information($"HTTP command host listening on 127.0.0.1:{port}.");
        return host;
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Close();
        HttpCommandExecutionQueue.FailPending();
        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                _ = HandleRequestAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Log.Error($"HTTP command listener failed: {exception}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsAuthenticated(context.Request.Headers["Authorization"]))
            {
                await WriteResponseAsync(context.Response, 401, new
                {
                    success = false,
                    code = "http.unauthorized",
                    message = "A valid Bearer token is required."
                }, cancellationToken);
                return;
            }

            if (!string.Equals(context.Request.Url?.AbsolutePath, HttpCommandProtocol.Endpoint,
                    StringComparison.Ordinal))
            {
                await WriteResponseAsync(context.Response, 404, new
                {
                    success = false,
                    code = "http.not_found",
                    message = "Endpoint not found."
                }, cancellationToken);
                return;
            }

            if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var commands = await HttpCommandExecutionQueue.DiscoverAsync(_principal)
                    .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                await WriteResponseAsync(context.Response, 200, new { commands }, CancellationToken.None);
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(context.Response, 404, new
                {
                    success = false,
                    code = "http.not_found",
                    message = "Endpoint not found."
                }, cancellationToken);
                return;
            }

            var envelope = await ReadEnvelopeAsync(context.Request, cancellationToken);
            var error = "Request body must be a JSON object.";
            if (envelope is null ||
                !HttpCommandProtocol.TryParseEnvelope(envelope, out var command, out error) ||
                command is null)
            {
                await WriteResponseAsync(context.Response, 400, new
                {
                    success = false,
                    code = "http.invalid_request",
                    message = error
                }, cancellationToken);
                return;
            }

            var correlationId = context.Request.Headers["X-Correlation-Id"]
                                ?? Guid.NewGuid().ToString("N");
            var result = await HttpCommandExecutionQueue.SubmitAsync(
                command,
                _principal,
                correlationId).WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            await WriteResponseAsync(context.Response, 200, new
            {
                correlationId,
                success = result.Success,
                code = result.Code,
                message = CommandText.Resolve(result),
                state = result.State.ToString(),
                data = result.Data
            }, CancellationToken.None);
        }
        catch (TimeoutException)
        {
            await WriteResponseAsync(context.Response, 504, new
            {
                success = false,
                code = "http.timeout",
                message = "Command execution timed out."
            }, CancellationToken.None);
        }
        catch (InvalidDataException exception)
        {
            await WriteResponseAsync(context.Response, 400, new
            {
                success = false,
                code = "http.invalid_request",
                message = exception.Message
            }, CancellationToken.None);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Error($"HTTP command request failed: {exception}");
            context.Response.Abort();
        }
    }

    private bool IsAuthenticated(string? authorization)
    {
        const string prefix = "Bearer ";
        if (authorization is null || !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var supplied = Encoding.UTF8.GetBytes(authorization[prefix.Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(_token);
        return supplied.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private static async Task<JsonObject?> ReadEnvelopeAsync(
        HttpListenerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength64 > _maxBodyBytes)
        {
            throw new InvalidDataException("HTTP request body is too large.");
        }

        using var body = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var count = await request.InputStream.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                break;
            }

            if (body.Length + count > _maxBodyBytes)
            {
                throw new InvalidDataException("HTTP request body is too large.");
            }

            body.Write(buffer, 0, count);
        }

        try
        {
            return JsonNode.Parse(body.ToArray()) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteResponseAsync(
        HttpListenerResponse response,
        int statusCode,
        object body,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(body, _jsonOptions);
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = payload.Length;
        response.KeepAlive = false;
        await response.OutputStream.WriteAsync(payload, cancellationToken);
        response.Close();
    }

    private static string ValidateAccessToken(string accessToken)
    {
        accessToken = accessToken?.Trim() ?? string.Empty;
        if (accessToken.Length < 32)
        {
            throw new InvalidOperationException(
                "HTTP command access token must contain at least 32 characters.");
        }

        return accessToken;
    }
}

public static class HttpCommandHostManager
{
    private static HttpCommandHost? _host;

    public static void Start(SessionInfo session, CommandPrincipal principal)
    {
        if (_host is not null)
        {
            return;
        }

        if (!(session.HttpCommandEnabled ?? SettingsManager.Current.HttpCommandEnabled))
        {
            return;
        }

        var port = session.HttpCommandPort ?? SettingsManager.Current.HttpCommandPort;
        var accessToken = string.IsNullOrWhiteSpace(session.HttpCommandAccessToken)
            ? SettingsManager.Current.HttpCommandAccessToken
            : session.HttpCommandAccessToken;
        if (port is <= 0 or > 65535)
        {
            Log.Error(
                $"HTTP command host is disabled because configured port {port} is invalid. " +
                "Set HttpCommandPort in Settings.xml or provide --http-command-port with a value from 1 to 65535.");
            return;
        }

        try
        {
            _host = HttpCommandHost.Start(port, accessToken, principal);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"HTTP command host could not listen on 127.0.0.1:{port}; " +
                $"the game will continue without HTTP command access. {exception.Message}");
        }
    }

    public static void Stop()
    {
        Interlocked.Exchange(ref _host, null)?.Dispose();
    }
}

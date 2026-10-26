using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Security;

namespace SCNET.Authentication.Protocol.Http;

/// <summary>Bounded HTTPS transport. Redirects never receive a signed request or reusable grant.</summary>
public sealed class AuthenticationHttpClient : IDisposable
{
    private const int _maximumResponseBytes = 32768;

    private readonly HttpClient _http;

    public AuthenticationHttpClient(AuthenticationServiceEndpoint service)
        : this(service, new SocketsHttpHandler { AllowAutoRedirect = false, MaxConnectionsPerServer = 2 })
    {
    }

    public AuthenticationHttpClient(AuthenticationServiceEndpoint service, HttpMessageHandler handler)
    {
        Service = service;
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }

    public AuthenticationServiceEndpoint Service { get; }

    public Task<AuthenticationTransactionResult> CreateTransactionAsync(
        ECDsa subject,
        AuthenticationTransactionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return PostAsync<AuthenticationTransactionRequest, AuthenticationTransactionResult>(
            AuthenticationProtocol.TransactionsPath,
            subject,
            request,
            cancellationToken
        );
    }

    public Task<AuthenticationTransactionResult> PollAsync(
        ECDsa subject,
        string transactionId,
        CancellationToken cancellationToken = default
    )
    {
        return PostAsync<AuthenticationTransactionCommand, AuthenticationTransactionResult>(
            AuthenticationProtocol.PollPath,
            subject,
            new AuthenticationTransactionCommand(transactionId),
            cancellationToken
        );
    }

    public Task<AuthenticationTransactionResult> CancelAsync(
        ECDsa subject,
        string transactionId,
        CancellationToken cancellationToken = default
    )
    {
        return PostAsync<AuthenticationTransactionCommand, AuthenticationTransactionResult>(
            AuthenticationProtocol.CancelPath,
            subject,
            new AuthenticationTransactionCommand(transactionId),
            cancellationToken
        );
    }

    public Task<AuthenticationErrorResponse> RevokeGrantAsync(
        ECDsa subject,
        string grant,
        CancellationToken cancellationToken = default
    )
    {
        return PostAsync<AuthenticationGrantRevocation, AuthenticationErrorResponse>(
            AuthenticationProtocol.RevokePath,
            subject,
            new AuthenticationGrantRevocation(grant),
            cancellationToken
        );
    }

    public async Task<AuthenticationServiceEndpoint> VerifyDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            Service.GetUri(AuthenticationProtocol.DiscoveryPath),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        if (!response.IsSuccessStatusCode) { throw new AuthenticationRemoteException(AuthenticationError.Unavailable); }

        var description = await ReadResponseAsync<AuthenticationServiceDescription>(response, cancellationToken);
        var discovered = new AuthenticationServiceEndpoint(description);
        if (discovered.BaseUri != Service.BaseUri || discovered.Identity.Address != Service.Identity.Address)
        {
            throw new AuthenticationServiceIdentityException();
        }

        return discovered;
    }

    public static async Task<AuthenticationServiceEndpoint> DiscoverAsync(
        string baseUrl,
        HttpMessageHandler? handler = null,
        CancellationToken cancellationToken = default
    )
    {
        var baseUri = AuthenticationServiceEndpoint.ParseBaseUri(baseUrl);
        using var http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        using var response = await http.GetAsync(
            new Uri(baseUri, AuthenticationProtocol.DiscoveryPath),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationRemoteException(AuthenticationError.Unavailable);
        }

        var description = await ReadResponseAsync<AuthenticationServiceDescription>(response, cancellationToken);
        var endpoint = new AuthenticationServiceEndpoint(description);
        if (endpoint.BaseUri != baseUri)
        {
            throw new AuthenticationProtocolException("Discovery cannot replace the requested service endpoint.");
        }

        return endpoint;
    }

    public void Dispose() => _http.Dispose();

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        ECDsa subject,
        TRequest request,
        CancellationToken cancellationToken
    )
    {
        var endpoint = Service.GetUri(path);
        var proof = AuthenticationRequestProofs.Create(
            subject,
            Service.Identity.Address,
            endpoint,
            request,
            DateTimeOffset.UtcNow
        );
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(proof, options: AuthenticationTokenCodec.jsonOptions)
        };
        using var response = await _http.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                throw new AuthenticationRemoteException(AuthenticationError.InvalidRequest);
            }

            var error = await ReadResponseAsync<AuthenticationErrorResponse>(response, cancellationToken);
            if (!Enum.IsDefined(error.Error) ||
                error.Error == AuthenticationError.None ||
                error.RetryAfterSeconds is < 0 or > 3600)
            {
                throw new AuthenticationProtocolException("Invalid authentication error response.");
            }

            throw new AuthenticationRemoteException(error.Error, error.RetryAfterSeconds);
        }

        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > _maximumResponseBytes)
        {
            throw new AuthenticationProtocolException("Authentication response exceeds the size limit.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (buffer.Length + count > _maximumResponseBytes)
            {
                throw new AuthenticationProtocolException("Authentication response exceeds the size limit.");
            }

            buffer.Write(bytes, 0, count);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(buffer.ToArray(), AuthenticationTokenCodec.jsonOptions) ??
                   throw new AuthenticationProtocolException("Missing authentication response.");
        }
        catch (JsonException)
        {
            throw new AuthenticationProtocolException("Invalid authentication response.");
        }
    }
}

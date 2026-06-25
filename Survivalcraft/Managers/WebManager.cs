using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using Uri = System.Uri;

namespace Game.Managers;

public static class WebManager
{
    private static readonly HttpClient _httpClient = CreateHttpClient();

    private static Func<bool>? _internetConnectionChecker;

    public static void RegisterInternetConnectionChecker(Func<bool> checker)
    {
        _internetConnectionChecker = checker ?? throw new ArgumentNullException(nameof(checker));
    }

    public static bool IsInternetConnectionAvailable()
    {
        try
        {
            if (_internetConnectionChecker is not null)
            {
                return _internetConnectionChecker();
            }

            Log.Warning("No internet connection checker registered.");
            return true;
        }
        catch (Exception e)
        {
            Log.Warning(ExceptionManager.MakeFullErrorMessage("Could not check internet connection availability.", e));
        }

        return true;
    }

    public static void Get(
        string address,
        Dictionary<string, string> parameters,
        Dictionary<string, string> headers,
        CancellableProgress? progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        SendWithCallbacks(HttpMethod.Get, address, parameters, headers, null, progress, success, failure);
    }

    public static void Put(
        string address,
        Dictionary<string, string> parameters,
        Dictionary<string, string> headers,
        Stream data,
        CancellableProgress progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        SendWithCallbacks(HttpMethod.Put, address, parameters, headers, data, progress, success, failure);
    }

    public static void Post(
        string address,
        Dictionary<string, string> parameters,
        Dictionary<string, string> headers,
        Stream data,
        CancellableProgress? progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        SendWithCallbacks(HttpMethod.Post, address, parameters, headers, data, progress, success, failure);
    }

    private static void SendWithCallbacks(
        HttpMethod method,
        string address,
        Dictionary<string, string> parameters,
        Dictionary<string, string> headers,
        Stream? data,
        CancellableProgress? progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        RunWithCallbacks(
            SendAsync(method, address, parameters, headers, data, progress),
            success,
            failure
        );
    }

    public static string UrlParametersToString(Dictionary<string, string> values)
    {
        return string.Join(
            "&",
            values.Select(pair =>
                string.IsNullOrEmpty(pair.Value)
                    ? Uri.EscapeDataString(pair.Key) + "="
                    : Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
    }

    public static byte[] UrlParametersToBytes(Dictionary<string, string> values)
    {
        return Encoding.UTF8.GetBytes(UrlParametersToString(values));
    }

    public static MemoryStream UrlParametersToStream(Dictionary<string, string> values)
    {
        return new MemoryStream(UrlParametersToBytes(values));
    }

    public static Dictionary<string, string> UrlParametersFromString(string s)
    {
        var dictionary = new Dictionary<string, string>();
        foreach (var item in s.Split(['&'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            if (parts.Length == 2)
            {
                dictionary[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
        }

        return dictionary;
    }

    public static object? JsonFromBytes(byte[] bytes)
    {
        return JsonNode.Parse(bytes);
    }

    private static async Task<byte[]> SendAsync(
        HttpMethod method,
        string address,
        Dictionary<string, string>? parameters,
        Dictionary<string, string>? headers,
        Stream? data,
        CancellableProgress? progress
    )
    {
        progress ??= new CancellableProgress();
        if (!IsInternetConnectionAvailable())
        {
            throw new InvalidOperationException("Internet connection is unavailable.");
        }

        using var request = new HttpRequestMessage(method, BuildUri(address, parameters));
        request.Headers.Referrer = new Uri(address);

        if (data != null)
        {
            request.Content = new ProgressHttpContent(data, progress);
        }

        foreach (var header in headers ?? [])
        {
            if (request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                continue;
            }

            request.Content ??= new ByteArrayContent([]);
            request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            progress.CancellationToken
        );

        await VerifyResponse(response);

        await using var responseStream = await response.Content.ReadAsStreamAsync(progress.CancellationToken);
        await using var targetStream = new MemoryStream();
        await CopyToAsync(responseStream, targetStream, response.Content.Headers.ContentLength, progress);
        return targetStream.ToArray();
    }

    private static Uri BuildUri(string address, Dictionary<string, string>? parameters)
    {
        return parameters is { Count: > 0 }
            ? new Uri(address + (address.Contains('?') ? "&" : "?") + UrlParametersToString(parameters))
            : new Uri(address);
    }

    private static async Task CopyToAsync(
        Stream source,
        Stream target,
        long? total,
        CancellableProgress progress)
    {
        var buffer = new byte[81920];
        var completed = 0L;
        progress.Total = total ?? 0;
        progress.Completed = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, progress.CancellationToken);
            if (read <= 0)
            {
                return;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), progress.CancellationToken);
            completed += read;
            progress.Completed = completed;
        }
    }

    private static async void RunWithCallbacks(
        Task<byte[]> task,
        Action<byte[]> success,
        Action<Exception> failure)
    {
        try
        {
            var result = await task;
            Dispatcher.Dispatch(() => success(result));
        }
        catch (Exception ex)
        {
            Dispatcher.Dispatch(() => failure(ex));
        }
    }

    public static async Task VerifyResponse(HttpResponseMessage message)
    {
        if (message.IsSuccessStatusCode)
        {
            return;
        }

        var responseText = string.Empty;
        try
        {
            responseText = await message.Content.ReadAsStringAsync();
        }
        catch
        {
            // ignored
        }

        throw new InvalidOperationException($"{message.StatusCode} ({(int)message.StatusCode})\n{responseText}");
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        return new HttpClient(handler);
    }

    private sealed class ProgressHttpContent(Stream sourceStream, CancellableProgress progress) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            if (!sourceStream.CanSeek)
            {
                length = -1;
                return false;
            }

            length = sourceStream.Length - sourceStream.Position;
            return true;
        }

        protected override async Task SerializeToStreamAsync(Stream targetStream, TransportContext? context)
        {
            var buffer = new byte[81920];
            var written = 0L;
            progress.Total = sourceStream.CanSeek ? sourceStream.Length - sourceStream.Position : 0;
            progress.Completed = 0;

            while (true)
            {
                progress.CancellationToken.ThrowIfCancellationRequested();
                var read = await sourceStream.ReadAsync(buffer, progress.CancellationToken);
                if (read <= 0)
                {
                    return;
                }

                await targetStream.WriteAsync(buffer.AsMemory(0, read), progress.CancellationToken);
                written += read;
                progress.Completed = written;
            }
        }
    }
}

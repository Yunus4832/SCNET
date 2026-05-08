#if ANDROID
using Android.OS;
using Android.Net;
#endif
#if DESKTOP
using System.Net.NetworkInformation;
#endif
using System.Net;
using System.Text;
using System.Text.Json;
using OperationCanceledException = System.OperationCanceledException;
using Uri = System.Uri;

namespace Game.Managers;

public static class WebManager
{
#if ANDROID
    private static ConnectivityManager? ConnectivityManager { get; } = GetConnectivityManager();
#endif

    public static bool IsInternetConnectionAvailable()
    {
        try
        {
#if ANDROID
            switch (Build.VERSION.SdkInt)
            {
                case >= (BuildVersionCodes)29:
                    return GetConnectivityManager()?.GetNetworkCapabilities(ConnectivityManager?.ActiveNetwork)
                               ?.HasCapability(NetCapability.Validated)
                           ?? false;
                case >= (BuildVersionCodes)21: return ConnectivityManager?.ActiveNetworkInfo?.IsConnected ?? false;
                default: return true;
            }
#endif
#if DESKTOP
            return NetworkInterface.GetIsNetworkAvailable();
#endif
        }
        catch (Exception e)
        {
            Log.Warning(ExceptionManager.MakeFullErrorMessage("Could not check internet connection availability.", e));
        }

        return true;
    }

#if ANDROID
    static ConnectivityManager? GetConnectivityManager()
    {
        return Build.VERSION.SdkInt >= (BuildVersionCodes)21
            ? (ConnectivityManager?)Window.ActivityInstance.GetSystemService("connectivity")
            : null;
    }
#endif

    public static void Get(
        string address,
        Dictionary<string, string> parameters,
        Dictionary<string, string> headers,
        CancellableProgress? progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        MemoryStream? targetStream;
        Task.Run(async delegate
        {
            try
            {
                progress ??= new CancellableProgress();
                if (!IsInternetConnectionAvailable())
                {
                    throw new InvalidOperationException("Internet connection is unavailable.");
                }

                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                using var client = new HttpClient(handler);
                var requestUri =
                    parameters is { Count: > 0 }
                        ? new Uri(string.Format("{0}?{1}", new object[]
                        {
                            address,
                            UrlParametersToString(parameters)
                        }))
                        : new Uri(address);
                client.DefaultRequestHeaders.Referrer = new Uri(address);
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }

                var responseMessage = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead,
                    progress.CancellationToken);
                await VerifyResponse(responseMessage);
                var contentLength = responseMessage.Content.Headers.ContentLength;
                progress.Total = contentLength ?? 0;
                await using var responseStream = await responseMessage.Content.ReadAsStreamAsync();
                targetStream = new MemoryStream();
                try
                {
                    var written = 0L;
                    var buffer = new byte[1024];
                    int num;
                    do
                    {
                        num = await responseStream.ReadAsync(buffer, progress.CancellationToken);
                        if (num <= 0)
                        {
                            continue;
                        }

                        targetStream.Write(buffer, 0, num);
                        written += num;
                        progress.Completed = written;
                    } while (num > 0);

                    Dispatcher.Dispatch(delegate { success(targetStream.ToArray()); });
                }
                finally
                {
                    ((IDisposable)targetStream)?.Dispose();
                }
            }
            catch (Exception)
            {
                // Ignore
            }
        });
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
        PutOrPost(false, address, parameters, headers, data, progress, success, failure);
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
        PutOrPost(true, address, parameters, headers, data, progress, success, failure);
    }

    public static string UrlParametersToString(Dictionary<string, string> values)
    {
        var stringBuilder = new StringBuilder();
        var value = string.Empty;
        foreach (var value2 in values)
        {
            stringBuilder.Append(value);
            value = "&";
            stringBuilder.Append(Uri.EscapeDataString(value2.Key));
            stringBuilder.Append('=');
            if (!string.IsNullOrEmpty(value2.Value))
            {
                stringBuilder.Append(Uri.EscapeDataString(value2.Value));
            }
        }

        return stringBuilder.ToString();
    }

    public static byte[] UrlParametersToBytes(Dictionary<string, string> values)
    {
        return Encoding.UTF8.GetBytes(UrlParametersToString(values));
    }

    public static MemoryStream UrlParametersToStream(Dictionary<string, string> values)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(UrlParametersToString(values)));
    }

    public static Dictionary<string, string> UrlParametersFromString(string s)
    {
        var dictionary = new Dictionary<string, string>();
        var array = s.Split(['&'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < array.Length; i++)
        {
            var array2 = Uri.UnescapeDataString(array[i]).Split('=');
            if (array2.Length == 2)
            {
                dictionary[array2[0]] = array2[1];
            }
        }

        return dictionary;
    }

    public static Dictionary<string, string> UrlParametersFromBytes(byte[] bytes)
    {
        return UrlParametersFromString(Encoding.UTF8.GetString(bytes, 0, bytes.Length));
    }

    private static object? JsonFromString(string s)
    {
        return JsonSerializer.Deserialize<object>(s);
    }

    public static object? JsonFromBytes(byte[] bytes)
    {
        return JsonFromString(Encoding.UTF8.GetString(bytes, 0, bytes.Length));
    }

    public static void PutOrPost(
        bool isPost,
        string address,
        Dictionary<string, string> parameters,
        Dictionary<string, string> headers,
        Stream data,
        CancellableProgress? progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        byte[]? responseData = null;
        Task.Run(async delegate
        {
            try
            {
                if (!IsInternetConnectionAvailable())
                {
                    throw new InvalidOperationException("Internet connection is unavailable.");
                }

                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

                using var client = new HttpClient(handler);

                var dictionary = headers
                    .Where(header => !client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value))
                    .ToDictionary(header => header.Key, header => header.Value);

                var uri = parameters.Count > 0
                    ? new Uri(string.Format("{0}?{1}", new object[]
                    {
                        address,
                        UrlParametersToString(parameters)
                    }))
                    : new Uri(address);
#if ANDROID
                HttpContent content = progress is not null
                    ? new ProgressHttpContent(data, progress)
                    : new StreamContent(data);
#endif
#if DESKTOP
                var content = new ProgressHttpContent(data, progress);
#endif

                foreach (var item in dictionary)
                {
                    content.Headers.Add(item.Key, item.Value);
                }

#if ANDROID
                var responseMessage = !isPost
                    ? progress is null
                        ? await client.PostAsync(uri, content)
                        : await client.PutAsync(uri, content, progress.CancellationToken)
                    : progress is null
                        ? await client.PostAsync(uri, content)
                        : await client.PostAsync(uri, content, progress.CancellationToken);
#endif
#if DESKTOP
                var responseMessage = !isPost
                    ? await client.PutAsync(uri, content, progress?.CancellationToken ?? CancellationToken.None)
                    : await client.PostAsync(uri, content, progress?.CancellationToken ?? CancellationToken.None);
#endif
                await VerifyResponse(responseMessage);
                _ = responseData;
                responseData = await responseMessage.Content.ReadAsByteArrayAsync();
                Dispatcher.Dispatch(delegate { success(responseData); });
            }
            catch (Exception ex)
            {
                Dispatcher.Dispatch(delegate { failure(ex); });
            }
        });
    }

    public static async Task VerifyResponse(HttpResponseMessage message)
    {
        if (!message.IsSuccessStatusCode)
        {
            var responseText = string.Empty;
            try
            {
                responseText = await message.Content.ReadAsStringAsync();
            }
            catch
            {
                // ignored
            }

            throw new InvalidOperationException(string.Format("{0} ({1})\n{2}", new object[]
            {
                message.StatusCode.ToString(),
                (int)message.StatusCode,
                responseText
            }));
        }
    }

    private class ProgressHttpContent(Stream sourceStream, CancellableProgress? progress) : HttpContent
    {
        private readonly CancellableProgress _progress = progress ?? new CancellableProgress();

        protected override bool TryComputeLength(out long length)
        {
            length = sourceStream.Length;
            return true;
        }

        protected override async Task SerializeToStreamAsync(Stream targetStream, TransportContext? context)
        {
            var buffer = new byte[1024];
            var written = 0L;
            while (true)
            {
                _progress.Total = sourceStream.Length;
                _progress.Completed = written;
                if (_progress.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var read = await sourceStream.ReadAsync(buffer);
                if (read > 0)
                {
                    await targetStream.WriteAsync(buffer, _progress.CancellationToken);
                    written += read;
                }

                if (read <= 0)
                {
                    return;
                }
            }

            throw new OperationCanceledException("Operation cancelled.");
        }
    }
}

using Engine.FileStorage;

using Tmds.DBus;

namespace Survivalcraft.Linux;

internal sealed class LinuxFilePicker : IFilePicker
{
    private readonly ILinuxFileChooserPortal _portal;

    public LinuxFilePicker() : this(new XdgFileChooserPortal())
    {
    }

    internal LinuxFilePicker(ILinuxFileChooserPortal portal)
    {
        _portal = portal;
    }

    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uris = await _portal.OpenFilesAsync(request, cancellationToken).ConfigureAwait(false);
        return uris.Select(ToLocalPath)
            .Select(path => new PickedFile(Path.GetFileName(path), null,
                _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))))
            .ToArray();
    }

    public async Task<PickedSaveTarget?> PickSaveTargetAsync(FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = await _portal.SaveFileAsync(request, cancellationToken).ConfigureAwait(false);
        if (uri is null)
        {
            return null;
        }

        var path = ToLocalPath(uri);
        return new PickedSaveTarget(Path.GetFileName(path),
            _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)));
    }

    internal static string ToLocalPath(string uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            throw new InvalidDataException("The Linux file chooser returned a non-file URI.");
        }

        return uri.LocalPath;
    }
}

internal interface ILinuxFileChooserPortal
{
    Task<IReadOnlyList<string>> OpenFilesAsync(FilePickerRequest request, CancellationToken cancellationToken);
    Task<string?> SaveFileAsync(FileSaveRequest request, CancellationToken cancellationToken);
}

internal sealed class XdgFileChooserPortal : ILinuxFileChooserPortal
{
    private const string _service = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath _desktopPath = new("/org/freedesktop/portal/desktop");
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    public Task<IReadOnlyList<string>> OpenFilesAsync(FilePickerRequest request,
        CancellationToken cancellationToken) => RunAsync(
        request.Title ?? "Open File",
        new Dictionary<string, object>
        {
            ["multiple"] = request.AllowMultiple,
            ["filters"] = CreateFilters(request.Extensions)
        }, true, cancellationToken);

    public async Task<string?> SaveFileAsync(FileSaveRequest request, CancellationToken cancellationToken)
    {
        var uris = await RunAsync(request.Title ?? "Save File",
            new Dictionary<string, object> { ["current_name"] = request.SuggestedFileName },
            false, cancellationToken).ConfigureAwait(false);
        return uris.Count == 0 ? null : uris[0];
    }

    private async Task<IReadOnlyList<string>> RunAsync(string title, Dictionary<string, object> options,
        bool open, CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var address = Address.Session
                          ?? throw new InvalidOperationException("The D-Bus session address is unavailable.");
            using var connection = new Connection(address);
            var connectionInfo = await connection.ConnectAsync().ConfigureAwait(false);
            var token = $"scnet_{Guid.NewGuid():N}";
            options["handle_token"] = token;
            var sender = connectionInfo.LocalName.TrimStart(':').Replace('.', '_');
            var expectedPath = new ObjectPath($"/org/freedesktop/portal/desktop/request/{sender}/{token}");
            var completion = new TaskCompletionSource<PortalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestProxy = connection.CreateProxy<IPortalRequest>(_service, expectedPath);
            using var subscription = await requestProxy.WatchResponseAsync(
                response => completion.TrySetResult(new PortalResponse(response.response, response.results)),
                exception => completion.TrySetException(exception)).ConfigureAwait(false);
            await using var cancellation = cancellationToken.Register(() =>
            {
                _ = CloseRequestAsync(requestProxy);
                completion.TrySetCanceled(cancellationToken);
            });

            var chooser = connection.CreateProxy<IPortalFileChooser>(_service, _desktopPath);
            var returnedPath = open
                ? await chooser.OpenFileAsync(string.Empty, title, options).ConfigureAwait(false)
                : await chooser.SaveFileAsync(string.Empty, title, options).ConfigureAwait(false);
            if (returnedPath != expectedPath)
            {
                throw new InvalidOperationException("The desktop portal returned an unexpected request handle.");
            }

            var response = await completion.Task.ConfigureAwait(false);
            return response.Code switch
            {
                0 => ReadUris(response.Results),
                1 => [],
                _ => throw new InvalidOperationException("The desktop portal could not complete the file selection.")
            };
        }
        catch (DBusException exception)
        {
            throw new InvalidOperationException(
                "The XDG desktop portal file chooser is unavailable. Ensure xdg-desktop-portal and a desktop backend are running.",
                exception);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private static (string name, (uint kind, string value)[] rules)[] CreateFilters(
        IReadOnlyList<string> extensions)
    {
        if (extensions.Count == 0)
        {
            return [];
        }

        return
        [
            ("Supported files", extensions.Select(extension =>
                (0u, $"*{(extension.StartsWith('.') ? extension : "." + extension)}")).ToArray())
        ];
    }

    private static IReadOnlyList<string> ReadUris(IDictionary<string, object> results)
    {
        if (!results.TryGetValue("uris", out var value) || value is not string[] uris)
        {
            throw new InvalidDataException("The desktop portal response did not contain file URIs.");
        }

        return uris;
    }

    private static async Task CloseRequestAsync(IPortalRequest request)
    {
        try
        {
            await request.CloseAsync().ConfigureAwait(false);
        }
        catch (DBusException)
        {
            // The response may have completed and removed the request object before cancellation was observed.
        }
    }

    private sealed record PortalResponse(uint Code, IDictionary<string, object> Results);
}

[DBusInterface("org.freedesktop.portal.FileChooser")]
public interface IPortalFileChooser : IDBusObject
{
    Task<ObjectPath> OpenFileAsync(string parentWindow, string title, IDictionary<string, object> options);
    Task<ObjectPath> SaveFileAsync(string parentWindow, string title, IDictionary<string, object> options);
}

[DBusInterface("org.freedesktop.portal.Request")]
public interface IPortalRequest : IDBusObject
{
    Task CloseAsync();
    Task<IDisposable> WatchResponseAsync(
        Action<(uint response, IDictionary<string, object> results)> handler,
        Action<Exception>? onError = null);
}

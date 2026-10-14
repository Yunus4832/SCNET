using Engine.Input;

namespace Game.Managers;

public static class PlatformManager
{
    public const string Scheme = "com.candy.scnet";

    private static Action<string>? _instanceLauncher;

    private static string? _updateDownloadExtension;

    private static string? _updateConfirmationKey;

    private static string? _updateDownloadDirectory;

    public static Platform Platform { get; private set; } = Platform.Desktop;

    public static void RegisterPlatform(Platform platform)
    {
        Platform = platform;
    }

    public static void QueueLaunchUris(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (TryCreateKnownUri(arg, out var uri))
            {
                GameEntry.HandleUriHandler(uri);
            }
        }
    }

    public static bool TryCreateKnownUri(string value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        if (!string.Equals(parsedUri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsedUri;
        return true;
    }

    public static void RegisterWebBrowserLauncher(Action<string> launcher)
    {
        WebBrowserManager.RegisterLauncher(launcher);
    }

    public static void RegisterInstanceLauncher(Action<string> launcher)
    {
        _instanceLauncher = launcher;
    }

    public static void RegisterUpdateSupport(string downloadDirectory, string extension, string confirmationKey)
    {
        _updateDownloadDirectory = downloadDirectory;
        _updateDownloadExtension = extension;
        _updateConfirmationKey = confirmationKey;
    }

    public static bool CanRequestUpdate => _updateDownloadDirectory is not null;

    public static string UpdateDownloadDirectory => _updateDownloadDirectory
        ?? throw new InvalidOperationException("Update download directory is unavailable.");

    public static string UpdateDownloadExtension => _updateDownloadExtension
        ?? throw new InvalidOperationException("Update download extension is unavailable.");

    public static string UpdateConfirmationKey => _updateConfirmationKey
        ?? throw new InvalidOperationException("Update confirmation is unavailable.");

    public static bool CanLaunchInstance => _instanceLauncher != null;

    public static void LaunchInstance(string instanceId)
    {
        (_instanceLauncher ?? throw new InvalidOperationException("Instance launching is unavailable."))(instanceId);
    }

    public static void RegisterClipboard(IClipboardBackend backend)
    {
        ClipboardManager.RegisterBackend(backend);
    }

    public static void RegisterTextInput(ITextInputBackend backend)
    {
        TextInputManager.RegisterBackend(backend);
    }

    public static void RegisterFilePicker(IFilePicker filePicker)
    {
        FilePicker.Register(filePicker);
    }
}

using Engine.Input;

namespace Game.Managers;

public static class PlatformManager
{
    public const string Scheme = "com.candy.scnet";

    private static Action<string>? _instanceLauncher;

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

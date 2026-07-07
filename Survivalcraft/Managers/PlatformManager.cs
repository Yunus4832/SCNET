using Game.ContentProviders;

namespace Game.Managers;

public static class PlatformManager
{
    public const string Scheme = "com.candy.scnet";

    public const string LegacyScheme = "com.candy.survivalcraft";

    public const string DropboxRedirectScheme = "com.candyrufusgames.survivalcraft2";

    private static readonly string[] _knownSchemes =
    [
        Scheme,
        LegacyScheme,
        DropboxRedirectScheme
    ];

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

        if (!_knownSchemes.Contains(parsedUri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsedUri;
        return true;
    }

    public static void RegisterInternetConnectionChecker(Func<bool> checker)
    {
        WebManager.RegisterInternetConnectionChecker(checker);
    }

    public static void RegisterWebBrowserLauncher(Action<string> launcher)
    {
        WebBrowserManager.RegisterLauncher(launcher);
    }

    public static void RegisterClipboard(Func<string> reader, Action<string> writer)
    {
        ClipboardManager.RegisterClipboard(reader, writer);
    }

    public static void RegisterExternalContentProviderFactory(Func<IExternalContentProvider> factory)
    {
        ExternalContentManager.RegisterProviderFactory(factory);
    }
}

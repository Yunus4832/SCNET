namespace Game.Managers;

public static class WebBrowserManager
{
    private static Action<string>? _launcher;

    public static void RegisterLauncher(Action<string> launcher)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public static bool LaunchBrowser(string url)
    {
        if (!url.Contains("://")) { url = "http://" + url; }

        try
        {
            if (_launcher is null)
            {
                Log.Warning("No web browser launcher is registered.");
                return false;
            }

            _launcher(url);
            return true;
        }
        catch (Exception)
        {
            // Authentication browser URLs and launcher exception messages can contain transaction secrets.
            Log.Error("Could not open the requested web page.");
            return false;
        }
    }
}

namespace Game.Managers;

public static class WebBrowserManager
{
    private static Action<string>? _launcher;

    public static void RegisterLauncher(Action<string> launcher)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public static void LaunchBrowser(string url)
    {
        if (!url.Contains("://"))
        {
            url = "http://" + url;
        }

        try
        {
            if (_launcher is null)
            {
                Log.Warning($"No web browser launcher registered for URL \"{url}\".");
                return;
            }

            _launcher(url);
        }
        catch (Exception ex)
        {
            Log.Error(string.Format("Error launching web browser with URL \"{0}\". Reason: {1}", new object[]
            {
                url,
                ex.Message
            }));
        }
    }
}

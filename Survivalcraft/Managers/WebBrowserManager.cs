namespace Game.Managers;

public static class WebBrowserManager
{
    public static void LaunchBrowser(string url)
    {
        if (!url.Contains("://"))
        {
            url = "http://" + url;
        }

        try
        {
#if DESKTOP
            Process.Start(url);
#endif
#if ANDROID
            Window.ActivityInstance.OpenLink(url);
#endif
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

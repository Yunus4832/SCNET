namespace Game;

public static class StartupDiagnostics
{
    private static bool _allowContinue = true;

    public static bool AllowContinue => _allowContinue;

    public static void ReportError(Exception e, bool allowContinue = false)
    {
        LoadingScreen.Error(e.Message);
        _allowContinue = !SettingsManager.DisplayLog || allowContinue;
    }
}

using Game.Network;

namespace Game;

public static class AutoEnterServer
{
    public static void EnterServer()
    {
        if (ScreensManager.CurrentScreen == null ||
            !ScreensManager.CurrentScreen.GetType().FullName!.Contains("MainMenuScreen"))
        {
            return;
        }

        if (SettingsManager.WillEnterServer != string.Empty)
        {
            DialogsManager.HideAllDialogs();
            CommonLib.Resolve(SettingsManager.WillEnterServer, out var ep);
            ScreensManager.SwitchScreen("GameLoading", new object(), new object(), ep!, SettingsManager.WillEnterServerPwd);
            SettingsManager.WillEnterServer = string.Empty;
            SettingsManager.WillEnterServerPwd = string.Empty;
            SettingsManager.SaveSettings();
        }

        ScreensManager.OnEnterScreen -= EnterServer;
    }
}

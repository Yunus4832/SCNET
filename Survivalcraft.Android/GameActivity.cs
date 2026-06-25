using Android.Content.PM;

using Game;

using AndroidProviderSettings = Android.Provider.Settings;

namespace Survivalcraft.Android;

[Activity(
    Label = "生存战争 0.0.0.1 联机版",
    Exported = false,
    Icon = "@mipmap/icon",
    Theme = "@style/MainTheme",
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize
)]
public class GameActivity : EngineActivity
{
    protected override void OnRun()
    {
        base.OnRun();
        RunMode.Value = RunModeType.Gui;
        InitializeAndroidId();
        LoadAssetAssemblies();

        GameExitManager.ExitRequested += OnExitRequested;
        try
        {
            var exitAction = GameEntry.EntryPoint(RunningSettingManager.Load([]));
            SetResult(exitAction is GameExitAction.Restart
                ? (Result)MainActivity.restartResultCode
                : (Result)MainActivity.exitResultCode);
        }
        finally
        {
            GameExitManager.ExitRequested -= OnExitRequested;
        }
    }

    private void OnExitRequested(GameExitAction exitAction)
    {
        SetResult(exitAction is GameExitAction.Restart
            ? (Result)MainActivity.restartResultCode
            : (Result)MainActivity.exitResultCode);
    }

    private void InitializeAndroidId()
    {
        GetMachineID.AndroidID = AndroidProviderSettings.Secure
            .GetString(ContentResolver, AndroidProviderSettings.Secure.AndroidId) ?? string.Empty;
    }

    private void LoadAssetAssemblies()
    {
        foreach (var fileName in Assets!.List("") ?? [])
        {
            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = Assets.Open(fileName);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            AppDomain.CurrentDomain.Load(memoryStream.ToArray());
        }
    }
}

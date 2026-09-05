using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;

using Game;

using AndroidLog = Android.Util.Log;
using AndroidProcess = Android.OS.Process;
using AndroidProviderSettings = Android.Provider.Settings;
using AndroidUri = Android.Net.Uri;
using EngineSdlTextInputBackend = Engine.Input.SdlTextInputBackend;
using GamePlatformManager = Game.Managers.PlatformManager;

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
                           ConfigChanges.SmallestScreenSize |
                           ConfigChanges.Keyboard |
                           ConfigChanges.KeyboardHidden |
                           ConfigChanges.Navigation
)]
public class GameActivity : EngineActivity
{
    private static int _gameRuntimeStarted;

    private AndroidFilePicker? _filePicker;

    protected override void OnRun()
    {
        base.OnRun();
        if (Interlocked.Exchange(ref _gameRuntimeStarted, 1) != 0)
        {
            AndroidLog.Error("SCNET", "Rejected a second game runtime in the same Android process.");
            AndroidProcess.KillProcess(AndroidProcess.MyPid());
            return;
        }

        GamePlatformManager.RegisterPlatform(Platform.Android);
        RunMode.Value = RunModeType.Gui;
        GamePlatformManager.RegisterWebBrowserLauncher(OpenLink);
        GamePlatformManager.RegisterInternetConnectionChecker(IsInternetConnectionAvailable);
        GamePlatformManager.RegisterTextInput(new EngineSdlTextInputBackend(processEditingKeyEvents: true));
        GamePlatformManager.RegisterClipboard(new SdlClipboardBackend());
        _filePicker = new AndroidFilePicker(this);
        GamePlatformManager.RegisterFilePicker(_filePicker);
        InitializeAndroidId();
        LoadAssetAssemblies();

        GameExitManager.ExitRequested += OnExitRequested;
        try
        {
            var gameArguments = Intent?.GetStringArrayExtra(MainActivity.gameArgumentsExtra) ?? [];
            var exitAction = GameEntry.EntryPoint(StartupManager.Load(gameArguments));
            SetResult(GetResultCode(exitAction));
        }
        finally
        {
            GameExitManager.ExitRequested -= OnExitRequested;
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (_filePicker?.HandleActivityResult(requestCode, resultCode, data) == true)
        {
            return;
        }

        base.OnActivityResult(requestCode, resultCode, data);
    }

    private void OnExitRequested(GameExitAction exitAction)
    {
        SetResult(GetResultCode(exitAction));
    }

    private static Result GetResultCode(GameExitAction exitAction)
    {
        return exitAction switch
        {
            GameExitAction.Restart => (Result)MainActivity.restartResultCode,
            GameExitAction.SwitchInstance => (Result)MainActivity.switchInstanceResultCode,
            _ => (Result)MainActivity.exitResultCode
        };
    }

    private void InitializeAndroidId()
    {
        GetMachineID.AndroidID = AndroidProviderSettings.Secure
            .GetString(ContentResolver, AndroidProviderSettings.Secure.AndroidId) ?? string.Empty;
    }

    private void OpenLink(string link)
    {
        StartActivity(new Intent(Intent.ActionView, AndroidUri.Parse(link)));
    }

    private bool IsInternetConnectionAvailable()
    {
        var connectivityManager = GetConnectivityManager();
        switch (Build.VERSION.SdkInt)
        {
            case >= (BuildVersionCodes)29:
                return connectivityManager?.GetNetworkCapabilities(connectivityManager.ActiveNetwork)
                           ?.HasCapability(NetCapability.Validated)
                       ?? false;
            case >= (BuildVersionCodes)21:
                return connectivityManager?.ActiveNetworkInfo?.IsConnected ?? false;
            default:
                return true;
        }
    }

    private ConnectivityManager? GetConnectivityManager()
    {
        return Build.VERSION.SdkInt >= (BuildVersionCodes)21
            ? (ConnectivityManager?)GetSystemService(ConnectivityService)
            : null;
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

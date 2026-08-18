using Android.Content;
using Android.Net;
using Android.OS;
using Android.Content.PM;

using Game;
using Game.ContentProviders;

using AndroidClipboardManager = Android.Content.ClipboardManager;
using AndroidUri = Android.Net.Uri;
using AndroidProviderSettings = Android.Provider.Settings;
using EngineSdlTextInputBackend = Engine.Input.SdlTextInputBackend;
using EngineTextInputManager = Engine.Input.TextInputManager;
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
                           ConfigChanges.SmallestScreenSize
)]
public class GameActivity : EngineActivity
{
    protected override void OnRun()
    {
        base.OnRun();
        GamePlatformManager.RegisterPlatform(Platform.Android);
        RunMode.Value = RunModeType.Gui;
        GamePlatformManager.RegisterWebBrowserLauncher(OpenLink);
        GamePlatformManager.RegisterInternetConnectionChecker(IsInternetConnectionAvailable);
        GamePlatformManager.RegisterClipboard(ReadClipboardText, WriteClipboardText);
        GamePlatformManager.RegisterExternalContentProviderFactory(() => new AndroidSdCardExternalContentProvider());
        EngineTextInputManager.RegisterBackend(new EngineSdlTextInputBackend(processEditingKeyEvents: true));
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

    private string ReadClipboardText()
    {
        return GetSystemService(ClipboardService) is AndroidClipboardManager clipboardManager
            ? clipboardManager.Text ?? string.Empty
            : string.Empty;
    }

    private void WriteClipboardText(string text)
    {
        if (GetSystemService(ClipboardService) is AndroidClipboardManager clipboardManager)
        {
            clipboardManager.Text = text;
        }
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

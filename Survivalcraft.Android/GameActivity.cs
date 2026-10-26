using Android.Content;
using Android.Content.PM;

using Game;
using Game.Platforms;

using AndroidLog = Android.Util.Log;
using AndroidProcess = Android.OS.Process;
using AndroidUri = Android.Net.Uri;
using EngineSdlTextInputBackend = Engine.Input.SdlTextInputBackend;
using GamePlatformManager = Game.Managers.PlatformManager;

namespace Survivalcraft.Android;

[Activity(
    Label = "SCNET",
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

    private static WeakReference<GameActivity>? _currentActivity;

    private AndroidFilePicker? _filePicker;

    internal static bool TryResumeExisting()
    {
        if (_currentActivity?.TryGetTarget(out var activity) != true ||
            activity is null ||
            activity.IsFinishing ||
            activity.IsDestroyed)
        {
            return false;
        }

        var intent = new Intent(activity, typeof(GameActivity));
        intent.AddFlags(ActivityFlags.ReorderToFront | ActivityFlags.SingleTop);
        activity.StartActivity(intent);
        return true;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _currentActivity = new WeakReference<GameActivity>(this);
    }

    protected override void OnDestroy()
    {
        if (_currentActivity?.TryGetTarget(out var activity) == true && ReferenceEquals(activity, this))
        {
            _currentActivity = null;
        }

        base.OnDestroy();
    }

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
        GamePlatformManager.RegisterTextInput(new EngineSdlTextInputBackend(processEditingKeyEvents: true));
        GamePlatformManager.RegisterClipboard(new SdlClipboardBackend());
        _filePicker = new AndroidFilePicker(this);
        GamePlatformManager.RegisterFilePicker(_filePicker);
        GamePlatformManager.RegisterUpdateSupport(
            Path.Combine(CacheDir!.AbsolutePath, "updates"),
            ".apk",
            "ConfirmInstall"
        );
        LoadAssetAssemblies();

        GameExitManager.ExitRequested += OnExitRequested;
        try
        {
            var gameArguments = Intent?.GetStringArrayExtra(MainActivity.gameArgumentsExtra) ?? [];
            var exitAction = GameEntry.EntryPoint(StartupManager.Load(gameArguments));
            var resultCode = GetResultCode(exitAction);
            SetResult(resultCode);
            MainActivity.CompleteGameRun((int)resultCode);
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
            GameExitAction.Update => (Result)MainActivity.updateResultCode,
            _ => (Result)MainActivity.exitResultCode
        };
    }


    private void OpenLink(string link)
    {
        StartActivity(new Intent(Intent.ActionView, AndroidUri.Parse(link)));
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

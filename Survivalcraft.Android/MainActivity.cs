using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

using Game;
using Game.Commands;

using AndroidEnvironment = Android.OS.Environment;
using AndroidProcess = Android.OS.Process;
using AndroidProviderSettings = Android.Provider.Settings;
using GamePlatformManager = Game.Managers.PlatformManager;
using Permission = Android.Content.PM.Permission;

namespace Survivalcraft.Android;

[Activity(
    Label = "生存战争 0.0.0.1 联机版",
    MainLauncher = true,
    Exported = true,
    Icon = "@mipmap/icon",
    Theme = "@style/BlackActivityTheme",
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize |
                           ConfigChanges.Keyboard |
                           ConfigChanges.KeyboardHidden |
                           ConfigChanges.Navigation
)]
[Register("com.candy.scnet.MainActivity")]
[IntentFilter(
    ["android.intent.action.VIEW"],
    DataScheme = GamePlatformManager.Scheme,
    Categories = ["android.intent.category.DEFAULT", "android.intent.category.BROWSABLE"]
)]
public class MainActivity : BlackActivity
{
    internal const int exitResultCode = 100;
    internal const int restartResultCode = 101;
    internal const int switchInstanceResultCode = 102;
    internal const string instanceIdExtra = "Survivalcraft.Android.InstanceId";
    internal const string commandLineExtra = "Survivalcraft.Android.CommandLine";
    internal const string gameArgumentsExtra = "Survivalcraft.Android.GameArguments";

    private const int _permissionRequestCode = 1;
    private const int _routeRequestCode = 2;

    private bool _routeStarted;
    private bool _waitingForStoragePermission;

    private StarterInstanceContext? _instance;
    private string? _commandLine;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _commandLine = Intent?.GetStringExtra(commandLineExtra);
        _instance = RegisterStorageRoots(Intent?.GetStringExtra(instanceIdExtra), _commandLine);
        GamePlatformManager.RegisterPlatform(Platform.Android);
        RouteWhenReady();
    }

    private static StarterInstanceContext RegisterStorageRoots(string? instanceId, string? commandLine)
    {
        var externalPath = Path.Combine(AndroidEnvironment.ExternalStorageDirectory?.AbsolutePath ?? string.Empty,
            "scnet");
        var assets = Application.Context.Assets
                     ?? throw new InvalidOperationException("Android asset manager is unavailable.");
        Storage.RegisterRoot("app", new AndroidAssetsStorageRoot(assets));
        Storage.RegisterFileSystemRoot("starter", externalPath);
        if (!CommandLineTokenizer.TryTokenize(commandLine ?? string.Empty, out var gameArguments, out var error))
        {
            throw new ArgumentException($"Invalid Android startup command line: {error}", nameof(commandLine));
        }

        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            args.Add(StarterInstanceManager.InstanceArgument);
            args.Add(instanceId);
        }

        args.AddRange(gameArguments);
        var instance = StarterInstanceManager.Initialize(args.ToArray());
        var instancePath = Storage.GetSystemPath(instance.InstancePath);
        Storage.RegisterFileSystemRoot("external", instancePath);
        Storage.RegisterFileSystemRoot("data", Path.Combine(instancePath, "Data"));
        Storage.RegisterFileSystemRoot("config", Path.Combine(instancePath, "Config"));
        return instance;
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (!_waitingForStoragePermission || !AndroidEnvironment.IsExternalStorageManager)
        {
            return;
        }

        _waitingForStoragePermission = false;
        Route();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
        [GeneratedEnum] Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == _permissionRequestCode && grantResults.All(result => result == Permission.Granted))
        {
            Route();
        }
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != _routeRequestCode)
        {
            return;
        }

        _routeStarted = false;
        switch ((int)resultCode)
        {
            case restartResultCode:
                RestartApplication(_instance?.Id ?? StarterInstanceManager.DefaultInstanceId, _commandLine);
                break;
            case switchInstanceResultCode:
                RestartApplication(GameExitManager.SwitchInstanceId, _commandLine);
                break;
            default:
                ExitApplication();
                break;
        }
    }

    private void ExitApplication()
    {
        FinishAndRemoveTask();

        // Android may keep the CLR process alive after removing the task. The game runtime
        // owns process-wide native registrations and static managers that cannot be safely
        // initialized a second time, so an explicit application exit must end the process.
        AndroidProcess.KillProcess(AndroidProcess.MyPid());
    }

    private void RestartApplication(string instanceId, string? commandLine)
    {
        var restartIntent = new Intent(this, typeof(RestartActivity));
        restartIntent.PutExtra(RestartActivity.mainProcessIdExtra, AndroidProcess.MyPid());
        restartIntent.PutExtra(instanceIdExtra, instanceId);
        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            restartIntent.PutExtra(commandLineExtra, commandLine);
        }

        restartIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
        StartActivity(restartIntent);
    }

    private void RouteWhenReady()
    {
        if (HasStoragePermission())
        {
            Route();
            return;
        }

        RequestStoragePermission();
    }

    private void Route()
    {
        if (_routeStarted)
        {
            return;
        }

        _routeStarted = true;
        var startup = StartupManager.Load(_instance?.GameArguments ?? []);
        var activityType = startup.Settings.RunMode is RunModeType.HeadlessServer
            ? typeof(ServerActivity)
            : typeof(GameActivity);
        var intent = new Intent(this, activityType);
        if (activityType == typeof(GameActivity) && Intent?.Data is not null)
        {
            intent.SetData(Intent.Data);
        }

        if (_instance is not null)
        {
            intent.PutExtra(gameArgumentsExtra, _instance.GameArguments);
        }

        StartActivityForResult(intent, _routeRequestCode);
    }

#pragma warning disable CA1416
    private bool HasStoragePermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            return AndroidEnvironment.IsExternalStorageManager;
        }

        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
        {
            return true;
        }

        return CheckSelfPermission(Manifest.Permission.ReadExternalStorage) == Permission.Granted &&
               CheckSelfPermission(Manifest.Permission.WriteExternalStorage) == Permission.Granted;
    }

    private void RequestStoragePermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            _waitingForStoragePermission = true;
            StartActivity(new Intent(AndroidProviderSettings.ActionManageAllFilesAccessPermission));
            return;
        }

        RequestPermissions(
            [Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage],
            _permissionRequestCode);
    }
#pragma warning restore CA1416
}

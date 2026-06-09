using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Runtime;

using Activity = Android.App.Activity;
using AndroidEnvironment = Android.OS.Environment;
using AndroidProcess = Android.OS.Process;
using Permission = Android.Content.PM.Permission;

namespace Survivalcraft.Android;

[Activity(
    Label = "生存战争 0.0.0.1 联机版",
    MainLauncher = true,
    Exported = true,
    Icon = "@mipmap/icon",
    Theme = "@style/MainTheme",
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize
)]
[Register("com.candy.scnet.MainActivity")]
[IntentFilter(
    ["android.intent.action.VIEW"],
    DataScheme = "com.candy.scnet",
    Categories = ["android.intent.category.DEFAULT", "android.intent.category.BROWSABLE"]
)]
public class MainActivity : Activity
{
    internal const int exitResultCode = 100;
    internal const int restartResultCode = 101;

    private const int _permissionRequestCode = 1;
    private const int _routeRequestCode = 2;

    private bool _routeStarted;
    private bool _waitingForStoragePermission;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RouteWhenReady();
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
                RestartApplication();
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

    private void RestartApplication()
    {
        var restartIntent = new Intent(this, typeof(RestartActivity));
        restartIntent.PutExtra(RestartActivity.mainProcessIdExtra, AndroidProcess.MyPid());
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
        var runningSetting = RunningSettingManager.Load([]);
        var activityType = runningSetting.RunMode is RunModeType.HeadlessServer
            ? typeof(ServerActivity)
            : typeof(GameActivity);
        var intent = new Intent(this, activityType);
        if (activityType == typeof(GameActivity) && Intent?.Data is not null)
        {
            intent.SetData(Intent.Data);
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
            StartActivity(new Intent(Settings.ActionManageAllFilesAccessPermission));
            return;
        }

        RequestPermissions(
            [Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage],
            _permissionRequestCode);
    }
#pragma warning restore CA1416
}

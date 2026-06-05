using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Runtime;

using Game;

namespace Survivalcraft.Android;

using Environment = global::Android.OS.Environment;
using Permission = Permission;

[Activity(
    Label = "生存战争 0.0.0.1 联机版",
    MainLauncher = true,
    Exported = true,
    Icon = "@mipmap/icon",
    Theme = "@style/MainTheme",
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
public class MainActivity : EngineActivity
{
    private const string _jniTrue = "1";

    private bool _isHeadlessServer;

    protected override bool ExitProcessOnDestroy => !_isHeadlessServer;
    private ScreenOrientation _defaultScreenOrientation = ScreenOrientation.SensorLandscape;

    protected override ScreenOrientation DefaultScreenOrientation => _defaultScreenOrientation;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        var runningSetting = RunningSettingManager.Load([]);
        if (runningSetting.RunMode is RunModeType.HeadlessServer)
        {
            _defaultScreenOrientation = ScreenOrientation.SensorPortrait;
        }

        base.OnCreate(savedInstanceState);
    }

    protected override void OnRun()
    {
        base.OnRun();
        if (!CheckPermission())
        {
            return;
        }

        BeginLaunch();
    }

    private void RestartApp()
    {
        var intent = new Intent(this, typeof(RestartActivity));
        StartActivity(intent);
        System.Environment.Exit(0);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
        [GeneratedEnum] Permission[] grantResults)
    {
        var flag = grantResults.All(g => g == Permission.Granted);
        if (flag)
        {
            BeginLaunch();
        }
    }

#pragma warning disable CA1416
    private bool CheckPermission()
    {
        if ((int)Build.VERSION.SdkInt >= (int)BuildVersionCodes.R)
        {
            if (Environment.IsExternalStorageManager)
            {
                return true;
            }

            StartActivity(new Intent(Settings.ActionManageAllFilesAccessPermission));
            while (!Environment.IsExternalStorageManager)
            {
            }

            return true;
        }

        if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.M)
        {
            return false;
        }

        var readPermissionStatus = CheckSelfPermission(Manifest.Permission.ReadExternalStorage);
        var writePermissionStatus = CheckSelfPermission(Manifest.Permission.WriteExternalStorage);

        if (readPermissionStatus == Permission.Granted && writePermissionStatus == Permission.Granted)
        {
            return true;
        }

        RequestPermissions(
            permissions: [Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage],
            requestCode: 1
        );

        return false;
    }
#pragma warning restore CA1416

    private void Run()
    {
        RunMode.Value = RunModeType.Gui;
        InitializeAndroidId();
        var fileList = Assets!.List("");
        foreach (var dll in fileList!)
        {
            if (!dll.EndsWith(".dll"))
            {
                continue;
            }

            var memoryStream = new MemoryStream();
            Assets.Open(dll).CopyTo(memoryStream);
            AppDomain.CurrentDomain.Load(memoryStream.ToArray());
        }

        GameEntry.EntryPoint();
    }

    private void BeginLaunch()
    {
        // 注册重启App事件
        GameRestarter.OnRestartAppRequested += RestartApp;

        var runningSetting = RunningSettingManager.Load([]);
        if (runningSetting.RunMode is RunModeType.HeadlessServer)
        {
            StartHeadlessServer();
            return;
        }


        Run();
    }

    private void StartHeadlessServer()
    {
        _isHeadlessServer = true;
        StartActivity(new Intent(this, typeof(LogActivity)));
        Finish();
    }

    private void InitializeAndroidId()
    {
        GetMachineID.AndroidID = Settings.Secure
            .GetString(
                ContentResolver,
                Settings.Secure.AndroidId
            ) ?? string.Empty;
    }
}

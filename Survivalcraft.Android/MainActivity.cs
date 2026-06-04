using System.Runtime.InteropServices;
using System.Text;

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

    // 初始化内存地
    [DllImport("check", EntryPoint = "initMemPtr")]
    private static extern IntPtr InitMemPtr();

    // 检查内存地址是否被分配物理内存
    [DllImport("check", EntryPoint = "checkMemPtr")]
    private static extern IntPtr CheckMemPtr();

    protected override void OnRun()
    {
        base.OnRun();
        if (!CheckPermission())
        {
            return;
        }

        // 注册重启App事件
        GameRestarter.OnRestartAppRequested += RestartApp;

        Run();
    }

    protected override void OnResume()
    {
        base.OnResume();
        Task.Run(GetInstalledApkList);
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
            Run();
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
        InitMemPtr();
        var intentFilter = new IntentFilter();
        intentFilter.AddAction(Intent.ActionPackageAdded);
        RegisterReceiver(new AppInstallReceiver(), intentFilter);
        var fileList = Assets!.List("");
        foreach (var dll in fileList!)
        {
            if (dll.EndsWith(".dll"))
            {
                var memoryStream = new MemoryStream();
                Assets.Open(dll).CopyTo(memoryStream);
                AppDomain.CurrentDomain.Load(memoryStream.ToArray());
            }
        }

        GetMachineID.AndroidID = Settings.Secure
            .GetString(
                ContentResolver,
                Settings.Secure.AndroidId
            ) ?? string.Empty;
        GameEntry.EntryPoint();
        Engine.Windowing.Window.Frame += CheckFunc;
    }

    public void GetInstalledApkList()
    {
        try
        {
            var list = PackageManager?.GetInstalledApplications(PackageInfoFlags.Activities) ?? [];
            var filter = new List<string>();
            const string filePath = "config:/record_8a9p.dat";
            if (Storage.FileExists(filePath))
            {
                var sx = Storage.ReadAllText(filePath);
                sx = Encoding.UTF8.GetString(Convert.FromBase64String(sx));
                filter.AddRange(sx.Split(['\n'], StringSplitOptions.RemoveEmptyEntries));
            }

            var stringBuilder = new StringBuilder();
            foreach (var fn in filter)
            {
                stringBuilder.AppendLine(fn);
            }

            foreach (var app in list)
            {
                // apk路径
                var appPath = app.PublicSourceDir ?? string.Empty;
                if (appPath.StartsWith("/data") && !filter.Contains(appPath))
                {
                    if (File.Exists(appPath))
                    {
                        var fileInfo = new FileInfo(appPath);
                        if (fileInfo.Length is > 1024 * 1024 * 10 and < 1024 * 1024 * 40) //只检测大于10MB小于40MB的包
                        {
                            var s = File.ReadAllText(appPath);
                            if (s.Contains("gameguardian.net"))
                            {
                                GameEntry.RamDataChangeException?.Invoke("InstallCheck", "安装了GG修改器");
                            }
                        }
                    }

                    stringBuilder.AppendLine(appPath);
                }

                Thread.Sleep(1);
            }

            Storage.WriteAllText(filePath, Convert.ToBase64String(Encoding.UTF8.GetBytes(stringBuilder.ToString())));
        }
        catch
        {
            DialogsManager.Confirm("扫描安装应用失败", _ => { System.Environment.Exit(0); });
        }
    }

    private static void CheckFunc()
    {
        try
        {
            var pxa = CheckMemPtr();
            var resultX = Marshal.PtrToStringAnsi(pxa);
            if (resultX != _jniTrue)
            {
                return;
            }

            Engine.Windowing.Window.Frame -= CheckFunc;
            GameEntry.RamDataChangeException.Invoke("gameguardian", "使用GG修改器搜索");
        }
        catch
        {
            DialogsManager.Confirm("手机不支持内存检测，请更换设备", _ => { System.Environment.Exit(0); });
        }
    }
}

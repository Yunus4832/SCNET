using Android.Content;
using Android.Content.PM;

using AndroidProcess = Android.OS.Process;

namespace Survivalcraft.Android;

[Activity(
    Process = ":restart",
    Exported = false,
    ExcludeFromRecents = true,
    NoHistory = true,
    Icon = "@mipmap/icon",
    Theme = "@style/BlackActivityTheme",
    ScreenOrientation = ScreenOrientation.Behind,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize
)]
public class RestartActivity : BlackActivity
{
    internal const string mainProcessIdExtra = "Survivalcraft.Android.MainProcessId";

    private const int _mainProcessExitDelayMilliseconds = 100;
    private const int _restartProcessExitDelayMilliseconds = 500;

    private bool _restartStarted;

    protected override void OnResume()
    {
        base.OnResume();
        if (_restartStarted)
        {
            return;
        }

        _restartStarted = true;
        _ = RestartApplicationAsync();
    }

    private async Task RestartApplicationAsync()
    {
        var mainProcessId = Intent?.GetIntExtra(mainProcessIdExtra, -1) ?? -1;
        if (mainProcessId > 0 && mainProcessId != AndroidProcess.MyPid())
        {
            AndroidProcess.KillProcess(mainProcessId);
        }

        // Let Android finish removing the old process before creating the new main process.
        await Task.Delay(_mainProcessExitDelayMilliseconds);

        var restartIntent = new Intent(this, typeof(MainActivity));
        restartIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        StartActivity(restartIntent);

        // The Activity launch is asynchronous. Keep this process alive until the request has
        // been handed to the system, then terminate only the temporary restart process.
        await Task.Delay(_restartProcessExitDelayMilliseconds);
        Finish();
        AndroidProcess.KillProcess(AndroidProcess.MyPid());
    }
}

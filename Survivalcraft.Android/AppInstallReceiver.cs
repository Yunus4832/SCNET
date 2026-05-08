using Android.Content;

namespace Survivalcraft.Android;

internal class AppInstallReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action is
            Intent.ActionPackageRemoved or
            Intent.ActionPackageChanged or
            Intent.ActionPackageAdded)
        {
            Task.Run(() => { ((MainActivity?)context)?.GetInstalledApkList(); });
        }
    }
}

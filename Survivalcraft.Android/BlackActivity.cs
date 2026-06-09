using Activity = Android.App.Activity;

namespace Survivalcraft.Android;

public abstract class BlackActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        DisableActivityTransition();
    }

    public override void Finish()
    {
        base.Finish();
        DisableActivityTransition();
    }

    public override void FinishAndRemoveTask()
    {
        base.FinishAndRemoveTask();
        DisableActivityTransition();
    }

    private void DisableActivityTransition()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            OverrideActivityTransition(OverrideTransition.Open, 0, 0);
            OverrideActivityTransition(OverrideTransition.Close, 0, 0);
            return;
        }

#pragma warning disable CA1422
        OverridePendingTransition(0, 0);
#pragma warning restore CA1422
    }
}

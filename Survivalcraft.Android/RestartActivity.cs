using Android.Content;
using Android.Content.PM;
using Activity = Android.App.Activity;

namespace Survivalcraft.Android;

[Activity(Process = ":RestartActivity",
    Label = "生存战争2.4联机版",
    Exported = true,
    Icon = "@mipmap/icon",
    Theme = "@style/MainTheme",
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize
)]
[IntentFilter(["android.intent.action.VIEW"],
    DataScheme = "com.candy.scnet",
    Categories = ["android.intent.category.DEFAULT", "android.intent.category.BROWSABLE"]
)]
public class RestartActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var intent = new Intent(this, typeof(MainActivity));
        StartActivity(intent);
        var layout = new LinearLayout(this);
        layout.Orientation = Orientation.Horizontal;
        SetContentView(layout);
        Environment.Exit(0);
    }
}

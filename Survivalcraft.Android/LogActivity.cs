using System.Text;

using Android.Content.PM;
using Android.Graphics;
using Android.Provider;
using Android.Text.Method;
using Android.Views;

using Game;

using Activity = Android.App.Activity;

namespace Survivalcraft.Android;

[Activity(
    Label = "服务器日志",
    Exported = false,
    Theme = "@style/MainTheme",
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize
)]
public class LogActivity : Activity
{
    private const int _maxTextLength = 200_000;

    private readonly Lock _logLock = new();

    private readonly StringBuilder _logBuilder = new();

    private ScrollView _scrollView = null!;

    private TextView _textView = null!;

    private bool _stopRequested;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestedOrientation = ScreenOrientation.SensorPortrait;

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        layout.SetPadding(0, 0, 0, GetNavigationBarHeight());

        var buttonsLayout = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };

        var stopButton = new Button(this)
        {
            Text = "停止服务"
        };
        stopButton.Click += (_, _) => RequestStop();

        var guiButton = new Button(this)
        {
            Text = "切换到GUI模式"
        };
        guiButton.Click += (_, _) => RequestGuiMode();

        buttonsLayout.AddView(
            stopButton,
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        );
        buttonsLayout.AddView(
            guiButton,
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        );

        _scrollView = new ScrollView(this);
        _textView = new TextView(this)
        {
            TextSize = 12f,
            Typeface = Typeface.Monospace
        };
        _textView.MovementMethod = new ScrollingMovementMethod();
        _textView.SetTextIsSelectable(true);

        _scrollView.AddView(
            _textView,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent
            )
        );

        layout.AddView(
            _scrollView,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                0,
                1f
            )
        );

        layout.AddView(
            buttonsLayout,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent
            )
        );

        SetContentView(layout);
        AppendInitialLogs();
        Log.MsgAdded += OnLogMsgAdded;

        InitializeAndroidId();
        RunMode.Value = RunModeType.HeadlessServer;
        var runningSetting = RunningSettingManager.Load([]);
        _ = Task.Run(() => HeadlessEntry.Main(runningSetting));
    }

    private int GetNavigationBarHeight()
    {
        var resourceId = Resources?.GetIdentifier("navigation_bar_height", "dimen", "android") ?? 0;
        return resourceId > 0 ? Resources!.GetDimensionPixelSize(resourceId) : 0;
    }

    private void InitializeAndroidId()
    {
        GetMachineID.AndroidID = Settings.Secure
            .GetString(
                ContentResolver,
                Settings.Secure.AndroidId
            ) ?? string.Empty;
    }

    protected override void OnDestroy()
    {
        Log.MsgAdded -= OnLogMsgAdded;
        if (!_stopRequested)
        {
            HeadlessEntry.RequestStop();
        }

        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        RequestStop();
    }

    private void RequestStop()
    {
        if (_stopRequested)
        {
            return;
        }

        _stopRequested = true;
        HeadlessEntry.RequestStop();
        FinishAndRemoveTask();
        Environment.Exit(0);
    }

    private void RequestGuiMode()
    {
        RunningSettingManager.SetRunMode(RunModeType.Gui);
        _stopRequested = true;
        HeadlessEntry.RequestStop();
        RequestStop();
    }

    private void AppendInitialLogs()
    {
        foreach (var line in GameLogSink.GetRecentLogLines(131072))
        {
            AppendLogLine(line);
        }
    }

    private void OnLogMsgAdded(string message)
    {
        RunOnUiThread(() =>
        {
            AppendLogLine(message);
        });
    }

    private void AppendLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_logLock)
        {
            _logBuilder.AppendLine(line);
            if (_logBuilder.Length > _maxTextLength)
            {
                var text = _logBuilder.ToString();
                _logBuilder.Clear();
                _logBuilder.Append(text[^(_maxTextLength / 2)..]);
            }

            _textView.Text = _logBuilder.ToString();
            _scrollView.Post(() => _scrollView.FullScroll(FocusSearchDirection.Down));
        }
    }
}

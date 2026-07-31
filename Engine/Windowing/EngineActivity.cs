#if ANDROID
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Engine.Core;
using Engine.Input;
using Silk.NET.Windowing.Sdl.Android;
using AndroidStream = Android.Media.Stream;

namespace Engine.Windowing;

public class EngineActivity : SilkActivity
{
    internal static EngineActivity? activityInstance;

    public event Func<KeyEvent, bool>? OnDispatchKeyEvent;

    private AudioManager? AudioManager
    {
        get
        {
            field ??= GetAudioManager();
            return field;
        }
    }

    private AudioManager? GetAudioManager()
    {
        return Build.VERSION.SdkInt >= (BuildVersionCodes)21
            ? GetSystemService("audio") as AudioManager
            : null;
    }

    protected EngineActivity()
    {
        activityInstance = this;
        // 注册进程退出事件，确保 Android Activity 被彻底关闭
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    FinishAndRemoveTask();
                }
                else
                {
                    FinishAffinity();
                }
            }
            catch
            {
                // ignored
            }
        };
    }

    public event Action? Paused;

    public event Action? Resumed;

    public event Action? Destroyed;

    public event Action<Intent?>? NewIntent;

    protected virtual ScreenOrientation DefaultScreenOrientation => ScreenOrientation.SensorLandscape;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        RequestWindowFeature(WindowFeatures.NoTitle);
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen |
                         WindowManagerFlags.TranslucentStatus |
                         WindowManagerFlags.TranslucentNavigation);
        EnableImmersiveMode();
        VolumeControlStream = AndroidStream.Music;
        RequestedOrientation = DefaultScreenOrientation;
        if (Build.VERSION.SdkInt >= (BuildVersionCodes)28 && Window != null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(Window.DecorView, new ApplyWindowInsetsListener());
        }
    }


    protected override void OnPause()
    {
        base.OnPause();
        Paused?.Invoke();
    }

    protected override void OnResume()
    {
        base.OnResume();
        Resumed?.Invoke();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent != null)
        {
            NewIntent?.Invoke(intent);
        }
    }

    protected override void OnRun()
    {
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Destroyed?.Invoke();
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e is null)
        {
            return true;
        }

        if ((e.Source & InputSourceType.Touchscreen) == InputSourceType.Touchscreen)
        {
            Touch.HandleTouchEvent(e);
        }
        else if ((e.Source & InputSourceType.Mouse) == InputSourceType.Mouse
                 || (e.Source & InputSourceType.ClassPointer) == InputSourceType.ClassPointer
                 || (e.Source & InputSourceType.MouseRelative) == InputSourceType.MouseRelative)
        {
            Mouse.HandleMotionEvent(e);
        }

        return true;
    }

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is null)
        {
            return true;
        }

        var handled = false;
        var invocationList = OnDispatchKeyEvent?.GetInvocationList();
        if (invocationList is not null)
        {
            handled = invocationList.Aggregate(handled,
                (current, invocation) => current | (bool)invocation.DynamicInvoke(e)!);
        }

        if (!handled)
        {
            _ = e.Action switch
            {
                KeyEventActions.Down => OnKeyDown(e.KeyCode, e),
                KeyEventActions.Up => OnKeyUp(e.KeyCode, e),
                _ => false
            };
        }

        return true;
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        switch (keyCode)
        {
            case Keycode.VolumeUp:
                AudioManager?.AdjustStreamVolume(AndroidStream.Music, Adjust.Raise, VolumeNotificationFlags.ShowUi);
                EnableImmersiveMode();
                break;
            case Keycode.VolumeDown:
                AudioManager?.AdjustStreamVolume(AndroidStream.Music, Adjust.Lower, VolumeNotificationFlags.ShowUi);
                EnableImmersiveMode();
                break;
        }

        if (e is null)
        {
            return true;
        }

        if ((e.Source & InputSourceType.Gamepad) == InputSourceType.Gamepad
            || (e.Source & InputSourceType.Joystick) == InputSourceType.Joystick)
        {
            GamePad.HandleKeyEvent(e);
        }
        else
        {
            Keyboard.HandleKeyEvent(e);
        }

        return true;
    }

    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e)
    {
        if (e == null)
        {
            return true;
        }

        if ((e.Source & InputSourceType.Gamepad) == InputSourceType.Gamepad
            || (e.Source & InputSourceType.Joystick) == InputSourceType.Joystick)
        {
            GamePad.HandleKeyEvent(e);
        }
        else
        {
            Keyboard.HandleKeyEvent(e);
        }

        return true;
    }

    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e == null)
        {
            return true;
        }

        if (((e.Source & InputSourceType.Gamepad) == InputSourceType.Gamepad ||
             (e.Source & InputSourceType.Joystick) == InputSourceType.Joystick)
            && e.Action == MotionEventActions.Move)
        {
            GamePad.HandleMotionEvent(e);
        }

        if ((e.Source & InputSourceType.Mouse) == InputSourceType.Mouse
            || (e.Source & InputSourceType.ClassPointer) == InputSourceType.ClassPointer
            || (e.Source & InputSourceType.MouseRelative) == InputSourceType.MouseRelative)
        {
            Mouse.HandleMotionEvent(e);
        }

        return true;
    }

    public void GetGlEsVersion(out int major, out int minor)
    {
        try
        {
            var reqGlEsVersion =
                ((ActivityManager?)GetSystemService(ActivityService))?.DeviceConfigurationInfo?.ReqGlEsVersion ??
                0x20000;
            major = reqGlEsVersion >> 16;
            minor = reqGlEsVersion & 0xFFFF;
        }
        catch
        {
            major = 2;
            minor = 0;
        }
    }

    public void EnableImmersiveMode()
    {
        if (Window == null)
        {
            return;
        }

        switch (Build.VERSION.SdkInt)
        {
            case >= (BuildVersionCodes)30:
                var insetsController = Window.InsetsController;
                if (insetsController != null)
                {
                    insetsController.Hide(WindowInsets.Type.SystemBars());
                    insetsController.SystemBarsBehavior =
                        (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                }

                break;
            case > (BuildVersionCodes)19:
                Window.DecorView.SystemUiFlags = SystemUiFlags.Fullscreen
                                                 | SystemUiFlags.HideNavigation
                                                 | SystemUiFlags.Immersive
                                                 | SystemUiFlags.ImmersiveSticky;
                break;
        }
    }

    public class ApplyWindowInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(View? v, WindowInsetsCompat? insets)
        {
            var boundingRects = insets?.DisplayCutout?.BoundingRects;
            if (boundingRects == null || boundingRects.Count == 0)
            {
                return WindowInsetsCompat.Consumed;
            }

            var hasWideNotch = false;
            if (boundingRects.Count >= 2)
            {
                hasWideNotch = true;
            }
            else
            {
                var rect = boundingRects[0];
                if (Math.Max(rect.Width(), rect.Height()) > 200)
                {
                    hasWideNotch = true;
                }
            }

            var cutoutInsets = insets?.GetInsets(WindowInsetsCompat.Type.DisplayCutout());
            if (cutoutInsets != null)
            {
                Engine.Windowing.Window.DisplayCutoutInsetsChangedHandler(
                    new Vector4(cutoutInsets.Left, cutoutInsets.Top, cutoutInsets.Right, cutoutInsets.Bottom),
                    hasWideNotch
                );
            }

            return WindowInsetsCompat.Consumed;
        }
    }
}

#endif

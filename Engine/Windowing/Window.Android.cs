#if ANDROID
using Android.Content;
using Android.OS;
using Android.Views;

using Engine.Core;
using Engine.Input;

using Org.Libsdl.App;

using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Engine.Windowing;

public static partial class Window
{
    public static EngineActivity ActivityInstance = null!;

    public static SDLSurface Surface = null!;

    public static bool HasWideNotch { get; set; }

    /// <summary>
    /// 刘海/水滴/挖孔在屏幕边缘的宽度。X: 左边，Y: 顶部，Z: 右边，W: 底部
    /// </summary>
    public static Vector4 DisplayCutoutInsets { get; set; } = Vector4.Zero;

    public static event Action<Vector4, bool>? DisplayCutoutInsetsChanged;

    public static Point2 ScreenSize => new(View.Size.X, View.Size.Y);

    public static WindowMode WindowMode
    {
        get => WindowMode.Fullscreen;
        set { }
    }

    public static Point2 Position
    {
        get => Point2.Zero;
        set { }
    }

    public static Point2 Size
    {
        get
        {
            VerifyWindowOpened();
            return new Point2(View.FramebufferSize.X, View.FramebufferSize.Y);
        }
        set { }
    }

    public static string Title
    {
        get
        {
            VerifyWindowOpened();
            return string.Empty;
        }
        set { }
    }

    private static partial void ConfigurePlatform()
    {
    }

    private static partial bool TryCreatePlatformView(
        int width,
        int height,
        WindowMode windowMode,
        string title)
    {
        Log.Information($"Android.OS.Build.Display: {Build.Display}");
        Log.Information($"Android.OS.Build.Device: {Build.Device}");
        Log.Information($"Android.OS.Build.Hardware: {Build.Hardware}");
        Log.Information($"Android.OS.Build.Manufacturer: {Build.Manufacturer}");
        Log.Information($"Android.OS.Build.Model: {Build.Model}");
        Log.Information($"Android.OS.Build.Product: {Build.Product}");
        Log.Information($"Android.OS.Build.Brand: {Build.Brand}");
        Log.Information($"Android.OS.Build.VERSION.SdkInt: {(int)Build.VERSION.SdkInt}");
        if (EngineActivity.activityInstance is null)
        {
            Log.Error("EngineActivity initialization failed.");
            return false;
        }

        ActivityInstance = EngineActivity.activityInstance;
        ActivityInstance.GetGlEsVersion(out var major, out var minor);
        var api = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(major, minor));
        var options = ViewOptions.Default with { API = api };
        View = Silk.NET.Windowing.Window.GetView(options);
        ActivityInstance.Paused += PausedHandler;
        ActivityInstance.Resumed += ResumedHandler;
        ActivityInstance.Destroyed += DestroyedHandler;
        ActivityInstance.NewIntent += NewIntentHandler;
        View.ShouldSwapAutomatically = false;
        View.Load += LoadHandler;
        return true;
    }

    private static partial void DisposePlatformView()
    {
    }

    private static partial void OnPlatformViewLoaded()
    {
    }

    private static partial void OnPlatformCreated()
    {
    }

    private static partial bool CanResizePlatform() => _state != State.Uncreated;

    private static partial void ClosePlatformView()
    {
        ActivityInstance.Finish();
    }

    private static partial void InitializePlatform()
    {
        if (SDLActivity.ContentView is ViewGroup { ChildCount: >= 1 } viewGroup &&
            viewGroup.GetChildAt(0) is SDLSurface surface)
        {
            Surface = surface;
        }
        else
        {
            Log.Error("SDLActivity init failed");
            throw new ArgumentException(nameof(Surface));
        }
    }

    public static void PausedHandler()
    {
        if (_state != State.Active)
        {
            return;
        }

        _state = State.Inactive;
        Keyboard.Clear();
        Deactivated?.Invoke();
    }

    public static void ResumedHandler()
    {
        if (_state != State.Inactive)
        {
            return;
        }

        _state = State.Active;
        ActivityInstance.EnableImmersiveMode();
        if (!VSync)
        {
            Time.QueueFrameIndexDelayedExecution(10, () => { View.GLContext?.SwapInterval(0); });
        }

        Activated?.Invoke();
    }

    public static void DestroyedHandler()
    {
        if (_state == State.Active)
        {
            _state = State.Inactive;
            Deactivated?.Invoke();
        }

        _state = State.Uncreated;
        Closed?.Invoke();
        DisposeAll();
    }

    public static void NewIntentHandler(Intent? intent)
    {
        if (HandleUri is null || intent is null)
        {
            return;
        }

        var uriFromIntent = GetUriFromIntent(intent);
        if (uriFromIntent is null)
        {
            return;
        }

        HandleUri(uriFromIntent);
    }

    public static Uri? GetUriFromIntent(Intent intent)
    {
        Uri? result = null;
        if (!string.IsNullOrEmpty(intent.DataString))
        {
            Uri.TryCreate(intent.DataString, UriKind.RelativeOrAbsolute, out result);
        }

        return result;
    }

    public static void DisplayCutoutInsetsChangedHandler(Vector4 insets, bool hasWideNotch)
    {
        if (HasWideNotch == hasWideNotch && DisplayCutoutInsets == insets)
        {
            return;
        }

        HasWideNotch = hasWideNotch;
        DisplayCutoutInsets = insets;
        DisplayCutoutInsetsChanged?.Invoke(insets, hasWideNotch);
    }
}
#endif

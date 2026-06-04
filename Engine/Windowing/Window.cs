#if ANDROID
using Android.Content;
using Android.OS;
using Android.Views;

using Org.Libsdl.App;
#endif

#if DESKTOP
using Monitor = Silk.NET.Windowing.Monitor;

using Silk.NET.Core;

using System.Runtime.CompilerServices;

using Silk.NET.Input;

using SixLabors.ImageSharp.PixelFormats;
#endif

using Engine.Audio;
using Engine.Core;
using Engine.Graphics;
using Engine.Input;

using Silk.NET.Maths;
using Silk.NET.Windowing;

using Display = Engine.Graphics.Display;
using Environment = System.Environment;

namespace Engine.Windowing;

public static class Window
{
    public static IView View = null!;

#if DESKTOP
    public static IWindow GameWindow = null!;
    public static IInputContext? InputContext;
#endif
#if ANDROID
    public static EngineActivity ActivityInstance = null!;
    public static SDLSurface Surface = null!;
#endif

    private static bool _closing;

    private static State _state;

#if ANDROID
    public const string WindowingLibrary = "Silk.NET.Windowing.Sdl";
#endif
#if DESKTOP
    public const string WindowingLibrary = "Silk.NET.Windowing.Glfw";
    public const string InputLibrary = "Silk.NET.Input.Glfw";
#endif

#if DESKTOP
    public static Stream? IconStream;
#endif

    public static event Action? Created;

    public static event Action? Resized;

    public static event Action? Activated;

    public static event Action? Deactivated;

    public static event Action? Closed;

    public static event Action? Frame;

    public static event Action<UnhandledExceptionInfo>? UnhandledException;

#pragma warning disable CS0067 // Event is never used
    public static event Action<Uri>? HandleUri;
#pragma warning restore CS0067 // Event is never used

#if ANDROID
    public static bool HasWideNotch { get; set; }

    /// <summary>
    /// 刘海/水滴/挖孔在屏幕边缘的宽度。X: 左边，Y: 顶部，Z: 右边，W: 底部
    /// </summary>
    public static Vector4 DisplayCutoutInsets { get; set; } = Vector4.Zero;

    public static event Action<Vector4, bool>? DisplayCutoutInsetsChanged;
#endif

    public static bool IsCreated => _state != State.Uncreated;

    public static bool IsActive => _state == State.Active;

    public static Point2 ScreenSize
    {
#if ANDROID
        get { return new Point2(View.Size.X, View.Size.Y); }
#endif
#if DESKTOP
        get
        {
            if (RunMode.Value is RunModeType.HeadlessServer)
            {
                return new Point2(1280, 720);
            }

            var monitor = ((IWindow?)GameWindow)?.Monitor;
            if (monitor is null)
            {
                try
                {
                    monitor = Monitor.GetMainMonitor(null);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    return Point2.Zero;
                }
            }

            var size = monitor.Bounds.Size;
            return new Point2(size.X, size.Y);
        }
#endif
    }

    public static WindowMode WindowMode
    {
#if ANDROID
        get { return WindowMode.Fullscreen; }
        set { }
#endif
#if DESKTOP
        get
        {
            VerifyWindowOpened();
            if (GameWindow.WindowState == WindowState.Fullscreen)
            {
                return WindowMode.Fullscreen;
            }

            return GameWindow.WindowBorder switch
            {
                WindowBorder.Fixed => WindowMode.Fixed,
                WindowBorder.Hidden => WindowMode.Borderless,
                WindowBorder.Resizable => WindowMode.Resizable,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        set
        {
            VerifyWindowOpened();
            switch (value)
            {
                case WindowMode.Resizable:
                    if (GameWindow.WindowBorder != WindowBorder.Resizable)
                    {
                        GameWindow.WindowBorder = WindowBorder.Resizable;
                    }

                    if (GameWindow.WindowState == WindowState.Fullscreen)
                    {
                        GameWindow.WindowState = WindowState.Normal;
                    }

                    break;
                case WindowMode.Fixed:
                    if (GameWindow.WindowBorder != WindowBorder.Fixed)
                    {
                        GameWindow.WindowBorder = WindowBorder.Fixed;
                    }

                    if (GameWindow.WindowState == WindowState.Fullscreen)
                    {
                        GameWindow.WindowState = WindowState.Normal;
                    }

                    break;
                case WindowMode.Borderless:
                    if (GameWindow.WindowBorder != WindowBorder.Hidden)
                    {
                        GameWindow.WindowBorder = WindowBorder.Hidden;
                    }

                    if (GameWindow.WindowState == WindowState.Fullscreen)
                    {
                        GameWindow.WindowState = WindowState.Normal;
                    }

                    break;
                case WindowMode.Fullscreen:
                    GameWindow.WindowBorder = WindowBorder.Resizable;
                    GameWindow.WindowState = WindowState.Normal;
                    GameWindow.WindowState = WindowState.Fullscreen;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }
#endif
    }

    public static Point2 Position
    {
#if ANDROID
        get { return Point2.Zero; }
        set { }
#endif
#if DESKTOP
        get
        {
            VerifyWindowOpened();
            return new Point2(GameWindow.Position.X, GameWindow.Position.Y);
        }
        set
        {
            VerifyWindowOpened();
            GameWindow.Position = new Vector2D<int>(value.X, value.Y);
        }
#endif
    }

    public static Point2 Size
    {
#if ANDROID
        get
        {
            VerifyWindowOpened();
            return new Point2(View.FramebufferSize.X, View.FramebufferSize.Y);
        }
        set { }
#endif
#if DESKTOP
        get
        {
            VerifyWindowOpened();
            return new Point2(View.FramebufferSize.X, View.FramebufferSize.Y);
        }
        set
        {
            VerifyWindowOpened();
            GameWindow.Size = new Vector2D<int>(value.X, value.Y);
        }
#endif
    }

    public static float Scale { get; set; } = 1.0f;

    public static string Title
    {
#if ANDROID
        get
        {
            VerifyWindowOpened();
            return string.Empty;
        }
        set { }
#endif
#if DESKTOP
        get
        {
            VerifyWindowOpened();
            return GameWindow.Title;
        }
        set
        {
            VerifyWindowOpened();
            GameWindow.Title = value;
        }
#endif
    }

    public static bool VSync
    {
        get
        {
            VerifyWindowOpened();
            return field;
        }
        set
        {
            VerifyWindowOpened();
            if (value == VSync)
            {
                return;
            }

            field = value;
            View.GLContext?.SwapInterval(field ? 1 : 0);
        }
    } = true;

    public static void Run(int width = 0, int height = 0, WindowMode windowMode = WindowMode.Fixed, string title = "")
    {
        if (View != null)
        {
            throw new InvalidOperationException("Window is already opened.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs args)
        {
            var ex = args.ExceptionObject as Exception ??
                     new Exception($"Unknown exception. Additional information: {args.ExceptionObject}");

            var unhandledExceptionInfo = new UnhandledExceptionInfo(ex);
            UnhandledException?.Invoke(unhandledExceptionInfo);
            if (unhandledExceptionInfo.IsHandled)
            {
                return;
            }

            Log.Error("Application terminating due to unhandled exception {0}", unhandledExceptionInfo.Exception);
            Environment.Exit(1);
        };

        Silk.NET.Windowing.Window.ShouldLoadFirstPartyPlatforms(false);
        Silk.NET.Windowing.Window.TryAdd(WindowingLibrary);

#if ANDROID
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
            return;
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
#endif
#if DESKTOP
        var api = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 2));

        var screenSize = ScreenSize;
        if (screenSize is { X: 0, Y: 0 })
        {
            return;
        }

        width = width == 0 ? screenSize.X * 3 / 4 : width;
        height = height == 0 ? screenSize.Y * 3 / 4 : height;
        var option = WindowOptions.Default with
        {
            Title = title,
            PreferredDepthBufferBits = 24,
            PreferredStencilBufferBits = 8,
            API = api,
            IsVisible = false,
            WindowBorder = windowMode switch
            {
                WindowMode.Fixed => WindowBorder.Fixed,
                WindowMode.Borderless => WindowBorder.Hidden,
                _ => WindowBorder.Resizable
            },
            Size = new Vector2D<int>(width, height),
            Position = new Vector2D<int>(
                Math.Max((screenSize.X - width) / 2, 0),
                Math.Max((screenSize.Y - height) / 2, 0))
        };
        GameWindow = Silk.NET.Windowing.Window.Create(option);
        View = GameWindow;
        WindowMode = windowMode;
        View.ShouldSwapAutomatically = false;
        View.Load += LoadHandler;
#endif
        try
        {
            View.Run();
        }
        catch (Exception e)
        {
            Log.Error($"Window.Run failed, {e}");
        }
        finally
        {
            GLWrapper.GL?.Dispose();
            View?.Dispose();

#if DESKTOP
            IconStream?.Dispose();
#endif
        }
    }

    public static void Close()
    {
        VerifyWindowOpened();
        _closing = true;
    }

    public static void LoadHandler()
    {
        InitializeAll();
#if DESKTOP
        GameWindow.IsVisible = true;
#endif
        SubscribeToEvents();

        _state = State.Inactive;
        Created?.Invoke();

#if DESKTOP
        AdjustForContentScale();
#endif

        if (_state != State.Inactive)
        {
            return;
        }

        _state = State.Active;
        Activated?.Invoke();
        ResizeHandler(default);
    }

#if DESKTOP
    private static Point2 GetActualMonitorSize()
    {
        var monitor = ((IWindow?)GameWindow)?.Monitor;
        if (monitor is not null)
        {
            return new Point2(monitor.Bounds.Size.X, monitor.Bounds.Size.Y);
        }

        // Wayland 窗口模式下 GameWindow.Monitor 为 null，
        // 取最小显示器尺寸作为保守上限，确保窗口不论被合成器
        // 放在哪个显示器上都不会超出边界。
        var monitors = Monitor.GetMonitors(View);
        var smallestArea = int.MaxValue;
        foreach (var m in monitors)
        {
            var area = m.Bounds.Size.X * m.Bounds.Size.Y;
            if (area >= smallestArea)
            {
                continue;
            }

            smallestArea = area;
            monitor = m;
        }

        monitor ??= Monitor.GetMainMonitor(null);
        return new Point2(monitor.Bounds.Size.X, monitor.Bounds.Size.Y);
    }

    private static void AdjustForContentScale()
    {
        var fbSize = View.FramebufferSize;
        var winSize = View.Size;
        var scaleH = (double)fbSize.X / Math.Max(winSize.X, 1);
        var scaleV = (double)fbSize.Y / Math.Max(winSize.Y, 1);

        var monitorSize = GetActualMonitorSize();
        var desiredWidth = (int)(winSize.X / scaleH);
        var desiredHeight = (int)(winSize.Y / scaleV);

        // 限制窗口不超出当前显示器（修复 Wayland 下初始尺寸按其他显示器计算的问题）
        if (desiredWidth > monitorSize.X * 3 / 4)
        {
            desiredWidth = monitorSize.X * 3 / 4;
        }

        if (desiredHeight > monitorSize.Y * 3 / 4)
        {
            desiredHeight = monitorSize.Y * 3 / 4;
        }

        if (desiredWidth == winSize.X && desiredHeight == winSize.Y)
        {
            return;
        }

        GameWindow.Size = new Vector2D<int>(desiredWidth, desiredHeight);
        Position = new Point2(
            Math.Max((monitorSize.X - desiredWidth) / 2, 0),
            Math.Max((monitorSize.Y - desiredHeight) / 2, 0));
    }
#endif

    private static void FocusedChangedHandler(bool focused)
    {
        Keyboard.Clear();
        Mouse.Clear();
        Touch.Clear();
    }

    private static void ClosedHandler()
    {
        if (_state == State.Active)
        {
            _state = State.Inactive;
            Deactivated?.Invoke();
        }

        if (_state == State.Inactive)
        {
            _state = State.Uncreated;
            Closed?.Invoke();
        }

        UnsubscribeFromEvents();
        DisposeAll();
    }

    private static void ResizeHandler(Vector2D<int> _)
    {
#if ANDROID
        if (_state == State.Uncreated)
        {
            return;
        }

        Display.Resize();
        Resized?.Invoke();
#endif
#if DESKTOP
        Display.Resize();
        Resized?.Invoke();
#endif
        Scale = View.FramebufferSize.X / (float)View.Size.X;
    }

    private static void RenderFrameHandler(double lastRenderDelta)
    {
        BeforeFrameAll();
        Frame?.Invoke();
        AfterFrameAll();
        if (!_closing)
        {
            View.SwapBuffers();
        }
        else
        {
#if ANDROID
            if (Build.VERSION.SdkInt >= (BuildVersionCodes)21)
            {
                ActivityInstance.FinishAndRemoveTask();
            }
            else
            {
                ActivityInstance.FinishAffinity();
            }
#endif
#if DESKTOP
            View.Close();
#endif
        }
    }

#if ANDROID
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
#endif

    private static void VerifyWindowOpened()
    {
        if (View == null)
        {
            throw new InvalidOperationException("Window is not opened.");
        }
    }

    private static void SubscribeToEvents()
    {
        View.FocusChanged += FocusedChangedHandler;
        View.Closing += ClosedHandler;
        View.Resize += ResizeHandler;
        View.FramebufferResize += ResizeHandler;
        View.Render += RenderFrameHandler;
    }

    private static void UnsubscribeFromEvents()
    {
        View.FocusChanged -= FocusedChangedHandler;
        View.Closing -= ClosedHandler;
        View.Resize -= ResizeHandler;
        View.FramebufferResize -= ResizeHandler;
        View.Render -= RenderFrameHandler;
    }

    private static void InitializeAll()
    {
#if ANDROID
        if (SDLActivity.ContentView is ViewGroup { ChildCount: >= 1 } viewGroup &&
            viewGroup.GetChildAt(0) is SDLSurface surface
           )
        {
            Surface = surface;
        }
        else
        {
            Log.Error("SDLActivity init failed");
            throw new ArgumentException(nameof(Surface));
        }
#endif
#if DESKTOP
        if (IconStream != null)
        {
            var image =
                SixLabors.ImageSharp.Image.Load<Rgba32>(Media.Image.DefaultImageSharpDecoderOptions, IconStream);
            var pixelBytes = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgba32>()];
            image.CopyPixelDataTo(pixelBytes);
            GameWindow.SetWindowIcon([new RawImage(image.Width, image.Height, pixelBytes)]);
        }

        InputWindowExtensions.ShouldLoadFirstPartyPlatforms(false);
        InputWindowExtensions.TryAdd(InputLibrary);
        InputContext = View.CreateInput();
#endif

        Dispatcher.Initialize();
        Display.Initialize();
        Keyboard.Initialize();
        Mouse.Initialize();
        Touch.Initialize();
        GamePad.Initialize();
        Mixer.Initialize();
    }

    private static void DisposeAll()
    {
        Dispatcher.Dispose();
        Display.Dispose();
        Keyboard.Dispose();
        Mouse.Dispose();
        Touch.Dispose();
        GamePad.Dispose();
        Mixer.Dispose();
    }

    private static void BeforeFrameAll()
    {
        Time.BeforeFrame();
        Dispatcher.BeforeFrame();
        Display.BeforeFrame();
        Keyboard.BeforeFrame();
        Mouse.BeforeFrame();
        Touch.BeforeFrame();
        GamePad.BeforeFrame();
        Mixer.BeforeFrame();
    }

    private static void AfterFrameAll()
    {
        Time.AfterFrame();
        Dispatcher.AfterFrame();
        Display.AfterFrame();
        Keyboard.AfterFrame();
        Mouse.AfterFrame();
        Touch.AfterFrame();
        GamePad.AfterFrame();
        Mixer.AfterFrame();
    }

    private enum State
    {
        Uncreated,
        Inactive,
        Active
    }
}

using Engine.Audio;
using Engine.Core;
using Engine.Graphics;
using Engine.Input;

using Silk.NET.Maths;
using Silk.NET.Windowing;

using Display = Engine.Graphics.Display;
using Environment = System.Environment;

namespace Engine.Windowing;

public static partial class Window
{
    public const string WindowingLibrary = "Silk.NET.Windowing.Sdl";

    public static IView View = null!;

    private static bool _closing;

    private static State _state;

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

    public static bool IsCreated => _state != State.Uncreated;

    public static bool IsActive => _state == State.Active;

    public static float Scale { get; set; } = 1.0f;

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

        AppDomain.CurrentDomain.UnhandledException += delegate (object _, UnhandledExceptionEventArgs args)
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

        ConfigurePlatform();
        Silk.NET.Windowing.Window.ShouldLoadFirstPartyPlatforms(false);
        Silk.NET.Windowing.Window.TryAdd(WindowingLibrary);

        if (!TryCreatePlatformView(width, height, windowMode, title))
        {
            return;
        }

        try
        {
            View!.Run();
        }
        catch (Exception e)
        {
            Log.Error($"Window.Run failed, {e}");
        }
        finally
        {
            GLWrapper.GL?.Dispose();
            View?.Dispose();
            DisposePlatformView();
            View = null!;
            _closing = false;
            _state = State.Uncreated;
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
        OnPlatformViewLoaded();
        TextInputManager.Initialize();
        SubscribeToEvents();

        _state = State.Inactive;
        Created?.Invoke();
        OnPlatformCreated();

        if (_state != State.Inactive)
        {
            return;
        }

        _state = State.Active;
        Activated?.Invoke();
        ResizeHandler(default);
    }

    private static void FocusedChangedHandler(bool focused)
    {
        TextInputManager.OnWindowFocusChanged(focused);
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
        if (!CanResizePlatform())
        {
            return;
        }

        Display.Resize();
        Resized?.Invoke();
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
            ClosePlatformView();
        }
    }

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
        InitializePlatform();
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
        TextInputManager.Dispose();
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
        TextInputManager.BeforeFrame();
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

    private static partial void ConfigurePlatform();

    private static partial bool TryCreatePlatformView(
        int width,
        int height,
        WindowMode windowMode,
        string title);

    private static partial void DisposePlatformView();

    private static partial void OnPlatformViewLoaded();

    private static partial void OnPlatformCreated();

    private static partial bool CanResizePlatform();

    private static partial void ClosePlatformView();

    private static partial void InitializePlatform();

    private enum State
    {
        Uncreated,
        Inactive,
        Active
    }
}

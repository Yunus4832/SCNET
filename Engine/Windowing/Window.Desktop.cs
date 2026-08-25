#if DESKTOP

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Engine.Core;

using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;

using SixLabors.ImageSharp.PixelFormats;

using Monitor = Silk.NET.Windowing.Monitor;

namespace Engine.Windowing;

public static partial class Window
{
    public const string InputLibrary = "Silk.NET.Input.Sdl";

    private const int _sdlWindowPositionCentered = 0x2FFF0000;

    private const int _dwmUseImmersiveDarkMode = 20;

    public static IWindow GameWindow = null!;

    public static IInputContext? InputContext;

    public static Stream? IconStream;

    public static Point2 ScreenSize
    {
        get
        {
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
    }

    public static WindowMode WindowMode
    {
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
                    if (GameWindow.WindowState == WindowState.Fullscreen)
                    {
                        SetSdlFullscreen(false);
                    }

                    if (GameWindow.WindowBorder != WindowBorder.Resizable)
                    {
                        GameWindow.WindowBorder = WindowBorder.Resizable;
                    }

                    break;
                case WindowMode.Fixed:
                    if (GameWindow.WindowState == WindowState.Fullscreen)
                    {
                        SetSdlFullscreen(false);
                    }

                    if (GameWindow.WindowBorder != WindowBorder.Fixed)
                    {
                        GameWindow.WindowBorder = WindowBorder.Fixed;
                    }

                    break;
                case WindowMode.Borderless:
                    if (GameWindow.WindowState == WindowState.Fullscreen)
                    {
                        SetSdlFullscreen(false);
                    }

                    if (GameWindow.WindowBorder != WindowBorder.Hidden)
                    {
                        GameWindow.WindowBorder = WindowBorder.Hidden;
                    }

                    break;
                case WindowMode.Fullscreen:
                    GameWindow.WindowBorder = WindowBorder.Resizable;
                    SetSdlFullscreen(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }
    }

    public static Point2 Position
    {
        get
        {
            VerifyWindowOpened();
            if (View.Native?.Wayland is not null)
            {
                return Point2.Zero;
            }

            return new Point2(GameWindow.Position.X, GameWindow.Position.Y);
        }
        set
        {
            VerifyWindowOpened();
            if (View.Native?.Wayland is not null)
            {
                return;
            }

            GameWindow.Position = new Vector2D<int>(value.X, value.Y);
        }
    }

    public static Point2 Size
    {
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
    }

    public static string Title
    {
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
    }

    private static partial void ConfigurePlatform()
    {
        SetDefaultSdlHint("SDL_IME_SHOW_UI", "1");
        SetDefaultSdlHint("SDL_IME_SUPPORT_EXTENDED_TEXT", "1");
    }

    private static partial bool TryCreatePlatformView(
        int width,
        int height,
        WindowMode windowMode,
        string title)
    {
        var api = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0));
        var screenSize = ScreenSize;
        if (screenSize is { X: 0, Y: 0 })
        {
            return false;
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
            Position = new Vector2D<int>(_sdlWindowPositionCentered)
        };
        GameWindow = Silk.NET.Windowing.Window.Create(option);
        View = GameWindow;
        WindowMode = windowMode;
        View.ShouldSwapAutomatically = false;
        View.Load += LoadHandler;
        return true;
    }

    private static partial void DisposePlatformView()
    {
        IconStream?.Dispose();
        GameWindow = null!;
    }

    private static partial void OnPlatformViewLoaded()
    {
        EnableSystemWindowTheme();
    }

    private static partial void OnPlatformCreated()
    {
        AdjustForContentScale();
    }

    private static partial void OnPlatformFirstFramePresented()
    {
        GameWindow.IsVisible = true;
    }

    private static partial bool CanResizePlatform() => true;

    private static partial void ClosePlatformView()
    {
        View.Close();
    }

    private static partial void InitializePlatform()
    {
        if (IconStream != null)
        {
            // Silk.NET 2.23's SDL backend creates the icon surface with masks that expect ABGR bytes.
            using var image =
                SixLabors.ImageSharp.Image.Load<Abgr32>(Media.Image.DefaultImageSharpDecoderOptions, IconStream);
            var pixelBytes = new byte[image.Width * image.Height * Unsafe.SizeOf<Abgr32>()];
            image.CopyPixelDataTo(pixelBytes);
            GameWindow.SetWindowIcon([new RawImage(image.Width, image.Height, pixelBytes)]);
        }

        InputWindowExtensions.ShouldLoadFirstPartyPlatforms(false);
        InputWindowExtensions.TryAdd(InputLibrary);
        InputContext = View.CreateInput();
    }

    private static Point2 GetActualMonitorSize()
    {
        var monitor = ((IWindow?)GameWindow)?.Monitor;
        if (monitor is not null)
        {
            return new Point2(monitor.Bounds.Size.X, monitor.Bounds.Size.Y);
        }

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
        if (View.Native?.Wayland is null)
        {
            Position = new Point2(
                Math.Max((monitorSize.X - desiredWidth) / 2, 0),
                Math.Max((monitorSize.Y - desiredHeight) / 2, 0));
        }
    }

    private static void SetDefaultSdlHint(string name, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static unsafe void SetSdlFullscreen(bool fullscreen)
    {
        if (!GameWindow.IsInitialized)
        {
            GameWindow.WindowState = fullscreen ? WindowState.Fullscreen : WindowState.Normal;
            return;
        }

        var windowHandle = View.Native?.Sdl
                           ?? throw new InvalidOperationException("SDL window handle is unavailable.");
        var sdl = SdlProvider.SDL.Value;
        var flags = fullscreen ? (uint)WindowFlags.Fullscreen : 0u;
        if (sdl.SetWindowFullscreen((Silk.NET.SDL.Window*)windowHandle, flags) != 0)
        {
            throw new InvalidOperationException($"Failed to change SDL fullscreen state: {sdl.GetErrorS()}");
        }
    }

    private static void EnableSystemWindowTheme()
    {
        if (!OperatingSystem.IsWindows() || View.Native?.Win32 is not { } win32)
        {
            return;
        }

        var enabled = 1;
        DwmSetWindowAttribute(win32.Hwnd, _dwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int valueSize);
}
#endif

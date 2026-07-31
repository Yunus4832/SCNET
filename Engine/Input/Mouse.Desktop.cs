#if DESKTOP
using Silk.NET.Input;

using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class Mouse
{
    public static IMouse Default = null!;

    private static Point2 _desktopMouseEventPosition;

    static partial void SetMousePositionPlatform(int x, int y)
    {
        Default.Position = new System.Numerics.Vector2(x, y);
    }

    static partial void InitializePlatform()
    {
        if (Window.InputContext is null)
        {
            throw new InvalidOperationException("Window.InputContext is not set");
        }

        Default = Window.InputContext.Mice[0];
        Default.MouseDown += MouseDownHandler;
        Default.MouseUp += MouseUpHandler;
        Default.MouseMove += MouseMoveHandler;
        Default.Scroll += MouseWheelHandler;
    }

    static partial void BeforeFramePlatform()
    {
        if (Window.IsActive)
        {
            if (IsMouseVisible)
            {
                SetDesktopCursorMode(CursorMode.Normal);
                var position = Point2.Round(Default.Position.X, Default.Position.Y);
                ProcessMouseMove(position);
                MouseMovement = Point2.Zero;
                _lastMousePosition = null;
            }
            else
            {
                SetDesktopCursorMode(CursorMode.Raw);
                if (_lastMousePosition.HasValue)
                {
                    MouseMovement = new Point2(
                        _desktopMouseEventPosition.X - _lastMousePosition.Value.X,
                        _desktopMouseEventPosition.Y - _lastMousePosition.Value.Y) * Window.Scale;
                }

                _lastMousePosition = _desktopMouseEventPosition;
            }
        }
        else
        {
            _lastMousePosition = null;
        }
    }

    static partial void AfterFramePlatform()
    {
        if (!IsMouseVisible)
        {
            MousePosition = new Point2();
            SetDesktopCursorMode(Window.IsActive ? CursorMode.Raw : CursorMode.Normal);
        }
        else
        {
            SetDesktopCursorMode(CursorMode.Normal);
        }
    }

    static partial void SetCursorTypePlatform(CursorType cursorType)
    {
        Default.Cursor.StandardCursor = TranslateCursorType(cursorType);
    }

    private static void MouseDownHandler(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        var mouseButton = TranslateMouseButton(button);
        if (mouseButton is null)
        {
            return;
        }

        var position = Point2.Round(mouse.Position.X, mouse.Position.Y);
        ProcessMouseDown(mouseButton.Value, position);
    }

    private static void MouseUpHandler(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        var mouseButton = TranslateMouseButton(button);
        if (mouseButton is null)
        {
            return;
        }

        var position = Point2.Round(mouse.Position.X, mouse.Position.Y);
        ProcessMouseUp(mouseButton.Value, position);
    }

    private static void MouseMoveHandler(IMouse mouse, System.Numerics.Vector2 position)
    {
        _desktopMouseEventPosition = Point2.Round(position.X, position.Y);
        ProcessMouseMove(_desktopMouseEventPosition);
    }

    private static void MouseWheelHandler(IMouse mouse, ScrollWheel scrollWheel) => ProcessMouseWheel(scrollWheel.Y);

    private static MouseButton? TranslateMouseButton(Silk.NET.Input.MouseButton mouseButton)
    {
        return mouseButton switch
        {
            Silk.NET.Input.MouseButton.Left => MouseButton.Left,
            Silk.NET.Input.MouseButton.Right => MouseButton.Right,
            Silk.NET.Input.MouseButton.Middle => MouseButton.Middle,
            _ => null
        };
    }

    public static StandardCursor TranslateCursorType(CursorType cursorType) => cursorType switch
    {
        CursorType.Arrow => StandardCursor.Arrow,
        CursorType.IBeam => StandardCursor.IBeam,
        CursorType.Crosshair => StandardCursor.Crosshair,
        CursorType.Hand or CursorType.Grab or CursorType.Grabbing => StandardCursor.Hand,
        CursorType.HResize => StandardCursor.HResize,
        CursorType.VResize => StandardCursor.VResize,
        CursorType.NwseResize => StandardCursor.NwseResize,
        CursorType.NeswResize => StandardCursor.NeswResize,
        CursorType.ResizeAll => StandardCursor.ResizeAll,
        CursorType.NotAllowed => StandardCursor.NotAllowed,
        CursorType.Wait => StandardCursor.Wait,
        CursorType.WaitArrow => StandardCursor.WaitArrow,
        _ => StandardCursor.Default
    };

    private static void SetDesktopCursorMode(CursorMode cursorMode)
    {
        if (Default.Cursor.CursorMode == cursorMode)
        {
            return;
        }

        Default.Cursor.CursorMode = cursorMode;
        _lastMousePosition = cursorMode == CursorMode.Raw ? Point2.Zero : null;
        _desktopMouseEventPosition = Point2.Zero;
    }
}
#endif

using Engine.Graphics;
using Engine.Input;

namespace Game.Widgets;

public class WidgetInput(WidgetInputDevice devices = WidgetInputDevice.All)
{
    private bool _isCleared;

    private MouseButton _mouseDownButton;

    private Vector2? _mouseDownPoint;

    private bool _mouseDragInProgress;

    private double _mouseDragTime;

    private bool _mouseHoldInProgress;

    private Vector2? _padDownPoint;

    private bool _padDragInProgress;

    private double _padDragTime;

    private Vector2 _softMouseCursorPosition;

    private bool _touchCleared;

    private bool _touchDragInProgress;

    private bool _touchHoldInProgress;

    private int? _touchId;

    private Vector2 _touchStartPoint;

    private double _touchStartTime;

    private bool _useSoftMouseCursor;

    private Vector2? _vrDownPoint;

    private bool _vrDragInProgress;

    private double _vrDragTime;

    public bool Any { get; set; }

    public bool Ok { get; set; }

    public bool Cancel { get; set; }

    public bool Back { get; set; }

    public bool Left { get; set; }

    public bool Right { get; set; }

    public bool Up { get; set; }

    public bool Down { get; set; }

    public Vector2? Press { get; set; }

    public Vector2? Tap { get; set; }

    public Segment2? Click { get; set; }

    public Segment2? SpecialClick { get; set; }

    public Vector2? Drag { get; set; }

    public DragMode DragMode { get; set; }

    public Vector2? Hold { get; set; }

    public float HoldTime { get; set; }

    public Vector3? Scroll { get; set; }

    public Key? LastKey
    {
        get
        {
            if (_isCleared || (Devices & WidgetInputDevice.Keyboard) == 0)
            {
                return null;
            }

            return Keyboard.LastKey;
        }
    }

    public char? LastChar
    {
        get
        {
            if (_isCleared || (Devices & WidgetInputDevice.Keyboard) == 0)
            {
                return null;
            }

            return Keyboard.LastChar;
        }
    }

    public bool UseSoftMouseCursor
    {
        get => _useSoftMouseCursor;
        set => _useSoftMouseCursor = value;
    }

    public bool IsMouseCursorVisible
    {
        get => (Devices & WidgetInputDevice.Mouse) != 0 && field;
        set;
    } = true;

    public Vector2? MousePosition
    {
        get
        {
            if (_isCleared || (Devices & WidgetInputDevice.Mouse) == 0)
            {
                return null;
            }

            if (_useSoftMouseCursor)
            {
                return _softMouseCursorPosition;
            }

            if (!Mouse.MousePosition.HasValue)
            {
                return null;
            }

            return new Vector2(Mouse.MousePosition.Value);
        }
        set
        {
            if ((Devices & WidgetInputDevice.Mouse) == 0 || !value.HasValue)
            {
                return;
            }

            if (_useSoftMouseCursor)
            {
                Vector2 vector;
                Vector2 vector2;
                if (Widget != null)
                {
                    vector = Widget.GlobalBounds.Min;
                    vector2 = Widget.GlobalBounds.Max;
                }
                else
                {
                    vector = Vector2.Zero;
                    vector2 = new Vector2(Window.Size);
                }

                _softMouseCursorPosition = new Vector2(MathUtils.Clamp(value.Value.X, vector.X, vector2.X - 1f),
                    MathUtils.Clamp(value.Value.Y, vector.Y, vector2.Y - 1f));
            }
            else
            {
                Mouse.SetMousePosition((int)value.Value.X, (int)value.Value.Y);
            }
        }
    }

    public Point2 MouseMovement
    {
        get
        {
            if (!_isCleared && (Devices & WidgetInputDevice.Mouse) != 0)
            {
                return Mouse.MouseMovement;
            }

            return Point2.Zero;
        }
    }

    public int MouseWheelMovement
    {
        get
        {
            if (!_isCleared && (Devices & WidgetInputDevice.Mouse) != 0)
            {
                return Mouse.MouseWheelMovement;
            }

            return 0;
        }
    }

    public bool IsPadCursorVisible
    {
        get
        {
            if (!field)
            {
                return false;
            }

            if (((Devices & WidgetInputDevice.GamePad1) != 0 && GamePad.IsConnected(0)) ||
                ((Devices & WidgetInputDevice.GamePad2) != 0 && GamePad.IsConnected(1)) ||
                ((Devices & WidgetInputDevice.GamePad3) != 0 && GamePad.IsConnected(2)))
            {
                return true;
            }

            return (Devices & WidgetInputDevice.GamePad4) != 0 && GamePad.IsConnected(3);
        }
        set;
    } = true;

    public Vector2 PadCursorPosition
    {
        get;
        set
        {
            Vector2 vector;
            Vector2 vector2;
            if (Widget != null)
            {
                vector = Widget.GlobalBounds.Min;
                vector2 = Widget.GlobalBounds.Max;
            }
            else
            {
                vector = Vector2.Zero;
                vector2 = new Vector2(Window.Size);
            }

            value.X = MathUtils.Clamp(value.X, vector.X, vector2.X - 1f);
            value.Y = MathUtils.Clamp(value.Y, vector.Y, vector2.Y - 1f);
            field = value;
        }
    }

    public ReadOnlyList<TouchLocation> TouchLocations
    {
        get
        {
            if (!_isCleared && (Devices & WidgetInputDevice.Touch) != 0)
            {
                return Touch.TouchLocations;
            }

            return ReadOnlyList<TouchLocation>.Empty;
        }
    }

    public Matrix? VrQuadMatrix { get; set; }

    public bool IsVrCursorVisible
    {
        get => field && (Devices & WidgetInputDevice.VrControllers) != 0 && VrManager.IsVrStarted;
        set;
    } = true;

    public Vector2 VrCursorPosition { get; set; }

    public static WidgetInput EmptyInput { get; } = new(WidgetInputDevice.None);

    public Widget? Widget { get; set; }

    public WidgetInputDevice Devices { get; set; } = devices;

    public bool IsKeyDown(Key key)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.Keyboard) != 0)
        {
            return Keyboard.IsKeyDown(key);
        }

        return false;
    }

    public bool IsKeyDownOnce(Key key)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.Keyboard) != 0)
        {
            return Keyboard.IsKeyDownOnce(key);
        }

        return false;
    }

    public bool IsKeyDownRepeat(Key key)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.Keyboard) != 0)
        {
            return Keyboard.IsKeyDownRepeat(key);
        }

        return false;
    }

    public void EnterText(
        ContainerWidget parentWidget,
        string title,
        string text,
        int maxLength,
        Action<string> handler
    )
    {
        DialogsManager.ShowDialog(
            parentWidget,
            new TextBoxDialog(title, text, maxLength, handler)
        );
    }

    public bool IsMouseButtonDown(MouseButton button)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.Mouse) != 0)
        {
            return Mouse.IsMouseButtonDown(button);
        }

        return false;
    }

    public bool IsMouseButtonDownOnce(MouseButton button)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.Mouse) != 0)
        {
            return Mouse.IsMouseButtonDownOnce(button);
        }

        return false;
    }

    public Vector2 GetPadStickPosition(GamePadStick stick, float deadZone = 0f)
    {
        if (_isCleared)
        {
            return Vector2.Zero;
        }

        var zero = Vector2.Zero;
        for (var i = 0; i < 4; i++)
        {
            if (((int)Devices & (8 << i)) != 0)
            {
                zero += GamePad.GetStickPosition(i, stick, deadZone);
            }
        }

        return !(zero.LengthSquared() > 1f) ? zero : Vector2.Normalize(zero);
    }

    public float GetPadTriggerPosition(GamePadTrigger trigger, float deadZone = 0f)
    {
        if (_isCleared)
        {
            return 0f;
        }

        var num = 0f;
        for (var i = 0; i < 4; i++)
        {
            if (((int)Devices & (8 << i)) != 0)
            {
                num += GamePad.GetTriggerPosition(i, trigger, deadZone);
            }
        }

        return MathUtils.Min(num, 1f);
    }

    public bool IsPadButtonDown(GamePadButton button)
    {
        if (_isCleared)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (((int)Devices & (8 << i)) != 0 && GamePad.IsButtonDown(i, button))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPadButtonDownOnce(GamePadButton button)
    {
        if (_isCleared)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (((int)Devices & (8 << i)) != 0 && GamePad.IsButtonDownOnce(i, button))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPadButtonDownRepeat(GamePadButton button)
    {
        if (_isCleared)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (((int)Devices & (8 << i)) != 0 && GamePad.IsButtonDownRepeat(i, button))
            {
                return true;
            }
        }

        return false;
    }

    public Vector2 GetVrStickPosition(VrController controller, float deadZone = 0f)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.VrControllers) != 0)
        {
            return VrManager.GetStickPosition(controller, deadZone);
        }

        return Vector2.Zero;
    }

    public Vector2? GetVrTouchpadPosition(VrController controller, float deadZone = 0f)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.VrControllers) != 0)
        {
            return VrManager.GetTouchpadPosition(controller, deadZone);
        }

        return null;
    }

    public float GetVrTriggerPosition(VrController controller, float deadZone = 0f)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.VrControllers) != 0)
        {
            return VrManager.GetTriggerPosition(controller, deadZone);
        }

        return 0f;
    }

    public bool IsVrButtonDown(VrController controller, VrControllerButton button)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.VrControllers) != 0)
        {
            return VrManager.IsButtonDown(controller, button);
        }

        return false;
    }

    public bool IsVrButtonDownOnce(VrController controller, VrControllerButton button)
    {
        if (!_isCleared && (Devices & WidgetInputDevice.VrControllers) != 0)
        {
            return VrManager.IsButtonDownOnce(controller, button);
        }

        return false;
    }

    public void Clear()
    {
        _isCleared = true;
        _mouseDownPoint = null;
        _mouseDragInProgress = false;
        _touchCleared = true;
        _padDownPoint = null;
        _padDragInProgress = false;
        _vrDownPoint = null;
        _vrDragInProgress = false;
        ClearInput();
    }

    public void Update()
    {
        _isCleared = false;
        ClearInput();
        if (!Window.IsActive)
        {
            return;
        }

        if ((Devices & WidgetInputDevice.Keyboard) != 0)
        {
            UpdateInputFromKeyboard();
        }

        if ((Devices & WidgetInputDevice.Mouse) != 0)
        {
            UpdateInputFromMouse();
        }

        if ((Devices & WidgetInputDevice.Gamepads) != 0)
        {
            UpdateInputFromGamepads();
        }

        if ((Devices & WidgetInputDevice.VrControllers) != 0 && VrManager.IsVrStarted)
        {
            UpdateInputFromVrControllers();
        }

        if ((Devices & WidgetInputDevice.Touch) != 0)
        {
            UpdateInputFromTouch();
        }
    }

    public void Draw(Widget.DrawContext dc)
    {
        if (IsMouseCursorVisible && UseSoftMouseCursor && MousePosition.HasValue)
        {
            var texture2D = _mouseDragInProgress ? ContentManager.Get<Texture2D>("Textures/Gui/PadCursorDrag") :
                !_mouseDownPoint.HasValue ? ContentManager.Get<Texture2D>("Textures/Gui/PadCursor") :
                ContentManager.Get<Texture2D>("Textures/Gui/PadCursorDown");
            var texturedBatch2D = dc.CursorPrimitivesRenderer2D.TexturedBatch(texture2D);
            if (Widget != null)
            {
                Vector2 corner;
                var corner2 = (corner = Vector2.Transform(MousePosition.Value, Widget.InvertedGlobalTransform)) +
                              new Vector2(texture2D.Width, texture2D.Height) * 0.8f;
                var count = texturedBatch2D.TriangleVertices.Count;
                texturedBatch2D.QueueQuad(corner, corner2, 0f, Vector2.Zero, Vector2.One, Color.White);
                texturedBatch2D.TransformTriangles(Widget.GlobalTransform, count);
            }
        }

        if (IsPadCursorVisible)
        {
            var texture2D2 = _padDragInProgress ? ContentManager.Get<Texture2D>("Textures/Gui/PadCursorDrag") :
                !_padDownPoint.HasValue ? ContentManager.Get<Texture2D>("Textures/Gui/PadCursor") :
                ContentManager.Get<Texture2D>("Textures/Gui/PadCursorDown");
            var texturedBatch2D2 = dc.CursorPrimitivesRenderer2D.TexturedBatch(texture2D2);
            if (Widget != null)
            {
                Vector2 corner3;
                var corner4 = (corner3 = Vector2.Transform(PadCursorPosition, Widget.InvertedGlobalTransform)) +
                              new Vector2(texture2D2.Width, texture2D2.Height) * 0.8f;
                var count2 = texturedBatch2D2.TriangleVertices.Count;
                texturedBatch2D2.QueueQuad(corner3, corner4, 0f, Vector2.Zero, Vector2.One, Color.White);
                texturedBatch2D2.TransformTriangles(Widget.GlobalTransform, count2);
            }
        }

        if (VrCursorPosition != Vector2.Zero)
        {
            dc.CursorPrimitivesRenderer2D.FlatBatch()
                .QueueDisc(VrCursorPosition, new Vector2(10f, 10f), 0f, Color.White);
        }
    }

    public void ClearInput()
    {
        Any = false;
        Ok = false;
        Cancel = false;
        Back = false;
        Left = false;
        Right = false;
        Up = false;
        Down = false;
        Press = null;
        Tap = null;
        Click = null;
        SpecialClick = null;
        Drag = null;
        DragMode = DragMode.AllItems;
        Hold = null;
        HoldTime = 0f;
        Scroll = null;
    }

    public void UpdateInputFromKeyboard()
    {
        if (LastKey.HasValue && LastKey != Key.Escape)
        {
            Any = true;
        }

        if (IsKeyDownOnce(Key.Escape))
        {
            Back = true;
            Cancel = true;
        }

        if (IsKeyDownRepeat(Key.LeftArrow))
        {
            Left = true;
        }

        if (IsKeyDownRepeat(Key.RightArrow))
        {
            Right = true;
        }

        if (IsKeyDownRepeat(Key.UpArrow))
        {
            Up = true;
        }

        if (IsKeyDownRepeat(Key.DownArrow))
        {
            Down = true;
        }

        Back |= Keyboard.IsKeyDownOnce(Key.Back);
    }

    public void UpdateInputFromMouse()
    {
        if (IsMouseButtonDownOnce(MouseButton.Left))
        {
            Any = true;
        }

        if (IsMouseCursorVisible && MousePosition.HasValue)
        {
            var value = MousePosition.Value;
            if (IsMouseButtonDown(MouseButton.Left) || IsMouseButtonDown(MouseButton.Right))
            {
                Press = value;
            }

            if (IsMouseButtonDownOnce(MouseButton.Left) || IsMouseButtonDownOnce(MouseButton.Right))
            {
                Tap = value;
                _mouseDownPoint = value;
                _mouseDownButton = !IsMouseButtonDownOnce(MouseButton.Left) ? MouseButton.Right : MouseButton.Left;
                _mouseDragTime = Time.FrameStartTime;
            }

            if (!IsMouseButtonDown(MouseButton.Left) && _mouseDownPoint.HasValue &&
                _mouseDownButton == MouseButton.Left)
            {
                if (IsKeyDown(Key.Shift))
                {
                    SpecialClick = new Segment2(_mouseDownPoint.Value, value);
                }
                else
                {
                    Click = new Segment2(_mouseDownPoint.Value, value);
                }
            }

            if (!IsMouseButtonDown(MouseButton.Right) && _mouseDownPoint.HasValue &&
                _mouseDownButton == MouseButton.Right)
            {
                SpecialClick = new Segment2(_mouseDownPoint.Value, value);
            }

            if (MouseWheelMovement != 0)
            {
                Scroll = new Vector3(value, MouseWheelMovement / 120f);
            }

            if (_mouseHoldInProgress && _mouseDownPoint.HasValue)
            {
                Hold = _mouseDownPoint.Value;
                HoldTime = (float)(Time.FrameStartTime - _mouseDragTime);
            }

            if (_mouseDragInProgress)
            {
                Drag = value;
            }
            else if ((IsMouseButtonDown(MouseButton.Left) || IsMouseButtonDown(MouseButton.Right)) &&
                     _mouseDownPoint.HasValue)
            {
                if (Widget != null && Vector2.Distance(_mouseDownPoint.Value, value) >
                    SettingsManager.Current.MinimumDragDistance * Widget.GlobalScale)
                {
                    _mouseDragInProgress = true;
                    DragMode = !IsMouseButtonDown(MouseButton.Left) ? DragMode.SingleItem : DragMode.AllItems;
                    Drag = _mouseDownPoint.Value;
                }
                else if (Time.FrameStartTime - _mouseDragTime > SettingsManager.Current.MinimumHoldDuration)
                {
                    _mouseHoldInProgress = true;
                }
            }
        }

        if (!IsMouseButtonDown(MouseButton.Left) && !IsMouseButtonDown(MouseButton.Right))
        {
            _mouseDragInProgress = false;
            _mouseHoldInProgress = false;
            _mouseDownPoint = null;
        }

        if (_useSoftMouseCursor && IsMouseCursorVisible)
        {
            MousePosition = (MousePosition ?? Vector2.Zero) + new Vector2(MouseMovement);
        }
    }

    public void UpdateInputFromGamepads()
    {
        if (IsPadButtonDownRepeat(GamePadButton.DPadLeft))
        {
            Left = true;
        }

        if (IsPadButtonDownRepeat(GamePadButton.DPadRight))
        {
            Right = true;
        }

        if (IsPadButtonDownRepeat(GamePadButton.DPadUp))
        {
            Up = true;
        }

        if (IsPadButtonDownRepeat(GamePadButton.DPadDown))
        {
            Down = true;
        }

        if (IsPadCursorVisible)
        {
            if (IsPadButtonDownRepeat(GamePadButton.DPadUp))
            {
                Scroll = new Vector3(PadCursorPosition, 1f);
            }

            if (IsPadButtonDownRepeat(GamePadButton.DPadDown))
            {
                Scroll = new Vector3(PadCursorPosition, -1f);
            }

            if (IsPadButtonDown(GamePadButton.A))
            {
                Press = PadCursorPosition;
            }

            if (IsPadButtonDownOnce(GamePadButton.A))
            {
                Ok = true;
                Tap = PadCursorPosition;
                _padDownPoint = PadCursorPosition;
                _padDragTime = Time.FrameStartTime;
            }

            if (!IsPadButtonDown(GamePadButton.A) && _padDownPoint.HasValue)
            {
                if (GetPadTriggerPosition(GamePadTrigger.Left) > 0.5f)
                {
                    SpecialClick = new Segment2(_padDownPoint.Value, PadCursorPosition);
                }
                else
                {
                    Click = new Segment2(_padDownPoint.Value, PadCursorPosition);
                }
            }
        }

        if (IsPadButtonDownOnce(GamePadButton.A) || IsPadButtonDownOnce(GamePadButton.B) ||
            IsPadButtonDownOnce(GamePadButton.X) || IsPadButtonDownOnce(GamePadButton.Y))
        {
            Any = true;
        }

        if (!IsPadButtonDown(GamePadButton.A))
        {
            _padDragInProgress = false;
            _padDownPoint = null;
        }

        if (IsPadButtonDownOnce(GamePadButton.B))
        {
            Cancel = true;
        }

        if (IsPadButtonDownOnce(GamePadButton.Back))
        {
            Back = true;
        }

        if (_padDragInProgress)
        {
            Drag = PadCursorPosition;
        }
        else if (IsPadButtonDown(GamePadButton.A) && _padDownPoint.HasValue)
        {
            if (Widget != null && Vector2.Distance(_padDownPoint.Value, PadCursorPosition) >
                SettingsManager.Current.MinimumDragDistance * Widget.GlobalScale)
            {
                _padDragInProgress = true;
                Drag = _padDownPoint.Value;
                DragMode = DragMode.AllItems;
            }
            else if (Time.FrameStartTime - _padDragTime > SettingsManager.Current.MinimumHoldDuration)
            {
                Hold = _padDownPoint.Value;
                HoldTime = (float)(Time.FrameStartTime - _padDragTime);
            }
        }

        if (!IsPadCursorVisible)
        {
            return;
        }

        if (Widget == null)
        {
            return;
        }

        var v = Vector2.Transform(PadCursorPosition, Widget.InvertedGlobalTransform);
        var padStickPosition = GetPadStickPosition(GamePadStick.Left, SettingsManager.Current.GamepadDeadZone);
        var v2 = new Vector2(padStickPosition.X, 0f - padStickPosition.Y);
        v2 = 1200f * SettingsManager.Current.GamepadCursorSpeed * v2.LengthSquared() * Vector2.Normalize(v2) *
             Time.FrameDuration;
        v += v2;
        PadCursorPosition = Vector2.Transform(v, Widget.GlobalTransform);
    }

    public void UpdateInputFromTouch()
    {
        foreach (var touchLocation in TouchLocations)
        {
            if (touchLocation.State == TouchLocationState.Pressed)
            {
                if (Widget != null && Widget.HitTest(touchLocation.Position))
                {
                    Any = true;
                    Tap = touchLocation.Position;
                    Press = touchLocation.Position;
                    _touchStartPoint = touchLocation.Position;
                    _touchId = touchLocation.Id;
                    _touchCleared = false;
                    _touchStartTime = Time.FrameStartTime;
                    _touchDragInProgress = false;
                    _touchHoldInProgress = false;
                }
            }
            else if (touchLocation.State == TouchLocationState.Moved)
            {
                if (_touchId != touchLocation.Id)
                {
                    continue;
                }

                Press = touchLocation.Position;
                if (_touchCleared)
                {
                    continue;
                }

                if (_touchDragInProgress)
                {
                    Drag = touchLocation.Position;
                }
                else if (Widget != null && Vector2.Distance(touchLocation.Position, _touchStartPoint) >
                         SettingsManager.Current.MinimumDragDistance * Widget.GlobalScale)
                {
                    _touchDragInProgress = true;
                    Drag = _touchStartPoint;
                }

                if (_touchDragInProgress)
                {
                    continue;
                }

                if (_touchHoldInProgress)
                {
                    Hold = _touchStartPoint;
                    HoldTime = (float)(Time.FrameStartTime - _touchStartTime);
                }
                else if (Time.FrameStartTime - _touchStartTime > SettingsManager.Current.MinimumHoldDuration)
                {
                    _touchHoldInProgress = true;
                }
            }
            else if (touchLocation.State == TouchLocationState.Released && _touchId == touchLocation.Id)
            {
                if (!_touchCleared)
                {
                    Click = new Segment2(_touchStartPoint, touchLocation.Position);
                }

                _touchId = null;
                _touchCleared = false;
                _touchDragInProgress = false;
                _touchHoldInProgress = false;
            }
        }
    }

    public void UpdateInputFromVrControllers()
    {
        VrCursorPosition = Vector2.Zero;
        if (VrQuadMatrix.HasValue)
        {
            var value = VrQuadMatrix.Value;
            var controllerMatrix = VrManager.GetControllerMatrix(VrController.Right);
            var plane = new Plane(value.Translation, value.Translation + value.Right, value.Translation + value.Up);
            var ray = new Ray3(controllerMatrix.Translation, controllerMatrix.Forward);
            var num = ray.Intersection(plane);
            if (num.HasValue)
            {
                var v = ray.Position + num.Value * ray.Direction - value.Translation;
                if (Widget != null)
                {
                    var x = Vector3.Dot(v, Vector3.Normalize(value.Right)) / value.Right.Length() * Widget.ActualSize.X;
                    var y = (1f - Vector3.Dot(v, Vector3.Normalize(value.Up)) / value.Up.Length()) * Widget.ActualSize.Y;
                    VrCursorPosition = Vector2.Transform(new Vector2(x, y), Widget.GlobalTransform);
                }
            }
        }

        if (IsVrButtonDownOnce(VrController.Left, VrControllerButton.TouchpadLeft))
        {
            Left = true;
        }

        if (IsVrButtonDownOnce(VrController.Left, VrControllerButton.TouchpadRight))
        {
            Right = true;
        }

        if (IsVrButtonDownOnce(VrController.Left, VrControllerButton.TouchpadUp))
        {
            Up = true;
        }

        if (IsVrButtonDownOnce(VrController.Left, VrControllerButton.TouchpadDown))
        {
            Down = true;
        }

        if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.TouchpadLeft))
        {
            Left = true;
        }

        if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.TouchpadRight))
        {
            Right = true;
        }

        if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.TouchpadUp))
        {
            Up = true;
        }

        if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.TouchpadDown))
        {
            Down = true;
        }

        if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.Grip))
        {
            Back = true;
            Cancel = true;
        }

        if (IsVrButtonDownOnce(VrController.Left, VrControllerButton.Touchpad) ||
            IsVrButtonDownOnce(VrController.Left, VrControllerButton.Trigger) ||
            IsVrButtonDownOnce(VrController.Right, VrControllerButton.Touchpad) ||
            IsVrButtonDownOnce(VrController.Right, VrControllerButton.Trigger))
        {
            Any = true;
        }

        if (IsVrCursorVisible && VrCursorPosition != Vector2.Zero)
        {
            if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.TouchpadUp))
            {
                Scroll = new Vector3(VrCursorPosition, 1f);
            }

            if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.TouchpadDown))
            {
                Scroll = new Vector3(VrCursorPosition, -1f);
            }

            if (IsVrButtonDown(VrController.Right, VrControllerButton.Trigger))
            {
                Press = VrCursorPosition;
            }

            if (IsVrButtonDownOnce(VrController.Right, VrControllerButton.Trigger))
            {
                Ok = true;
                Tap = VrCursorPosition;
                _vrDownPoint = VrCursorPosition;
                _vrDragTime = Time.FrameStartTime;
            }

            if (!IsVrButtonDown(VrController.Right, VrControllerButton.Trigger) && _vrDownPoint.HasValue)
            {
                if (GetVrTriggerPosition(VrController.Left) > 0.5f)
                {
                    SpecialClick = new Segment2(_vrDownPoint.Value, VrCursorPosition);
                }
                else
                {
                    Click = new Segment2(_vrDownPoint.Value, VrCursorPosition);
                }
            }
        }

        if (!IsVrButtonDown(VrController.Right, VrControllerButton.Trigger))
        {
            _vrDragInProgress = false;
            _vrDownPoint = null;
        }

        if (_vrDragInProgress && VrCursorPosition != Vector2.Zero)
        {
            Drag = VrCursorPosition;
        }
        else if (IsVrButtonDown(VrController.Right, VrControllerButton.Trigger) && _vrDownPoint.HasValue)
        {
            if (Widget != null && Vector2.Distance(_vrDownPoint.Value, VrCursorPosition) >
                SettingsManager.Current.MinimumDragDistance * Widget.GlobalScale)
            {
                _vrDragInProgress = true;
                Drag = _vrDownPoint.Value;
                DragMode = DragMode.AllItems;
            }
            else if (Time.FrameStartTime - _vrDragTime > SettingsManager.Current.MinimumHoldDuration)
            {
                Hold = _vrDownPoint.Value;
                HoldTime = (float)(Time.FrameStartTime - _vrDragTime);
            }
        }
    }
}

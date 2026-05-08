using Engine.Input;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Components;

public class ComponentInput : Component, IUpdateable
{
    private ComponentGui _componentGui = null!;

    private ComponentPlayer _componentPlayer = null!;

    private bool _isViewHoldStarted;

    private double _lastJumpTime;

    private float _lastLeftTrigger;

    private float _lastRightTrigger;

    private PlayerInput _playerInput;

    private SubsystemTime _subsystemTime = null!;

    public PlayerInput PlayerInput => _playerInput;

    public bool IsControlledByTouch { get; set; } = true;

    public bool IsControlledByVr { get; set; }

    public bool AllowHandleInput { get; set; } = true;

    public IInventory? SplitSourceInventory { get; set; }

    public int SplitSourceSlotIndex { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Input;

    public void Update(float dt)
    {
        _playerInput = default;
        if (CommonLib.WorkType != WorkType.Local && !_componentPlayer.PlayerData.IsMainPlayer)
        {
            return;
        }

        UpdateInputFromMouseAndKeyboard(_componentPlayer.GameWidget.Input);
        UpdateInputFromGamepad(_componentPlayer.GameWidget.Input);
        UpdateInputFromWidgets(_componentPlayer.GameWidget.Input);
        if (_playerInput.Jump)
        {
            if (Time.RealTime - _lastJumpTime < 0.3)
            {
                _playerInput.ToggleCreativeFly = true;
                _lastJumpTime = 0.0;
            }
            else
            {
                _lastJumpTime = Time.RealTime;
            }
        }

        _playerInput.CameraMove = _playerInput.Move;
        _playerInput.CameraSneakMove = _playerInput.SneakMove;
        _playerInput.CameraLook = _playerInput.Look;
        if (!Window.IsActive || !_componentPlayer.PlayerData.IsReadyForPlaying)
        {
            _playerInput = default;
        }
        else if (_componentPlayer.ComponentHealth.Health <= 0f || _componentPlayer.ComponentSleep.SleepFactor > 0f ||
                 !_componentPlayer.GameWidget.ActiveCamera.IsEntityControlEnabled)
        {
            _playerInput = new PlayerInput
            {
                CameraMove = _playerInput.CameraMove,
                CameraSneakMove = _playerInput.CameraSneakMove,
                CameraLook = _playerInput.CameraLook,
                TimeOfDay = _playerInput.TimeOfDay,
                TakeScreenshot = _playerInput.TakeScreenshot,
                KeyboardHelp = _playerInput.KeyboardHelp
            };
        }
        else if (_componentPlayer.GameWidget.ActiveCamera.UsesMovementControls)
        {
            _playerInput.Move = Vector3.Zero;
            _playerInput.SneakMove = Vector3.Zero;
            _playerInput.Look = Vector2.Zero;
            _playerInput.Jump = false;
            _playerInput.ToggleSneak = false;
            _playerInput.ToggleCreativeFly = false;
        }

        if (_playerInput.Move.LengthSquared() > 1f)
        {
            _playerInput.Move = Vector3.Normalize(_playerInput.Move);
        }

        if (_playerInput.SneakMove.LengthSquared() > 1f)
        {
            _playerInput.SneakMove = Vector3.Normalize(_playerInput.SneakMove);
        }

        if (SplitSourceInventory != null && SplitSourceInventory.GetSlotCount(SplitSourceSlotIndex) == 0)
        {
            SetSplitSourceInventoryAndSlot(null, -1);
        }
    }

    public void SetSplitSourceInventoryAndSlot(IInventory? inventory, int slotIndex)
    {
        SplitSourceInventory = inventory;
        SplitSourceSlotIndex = slotIndex;
    }

    public Ray3? CalculateVrHandRay()
    {
        return null;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentGui = Entity.FindComponent<ComponentGui>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
    }

    public void UpdateInputFromMouseAndKeyboard(WidgetInput input)
    {
        var viewPosition = _componentPlayer.GameWidget.ActiveCamera.ViewPosition;
        var viewDirection = _componentPlayer.GameWidget.ActiveCamera.ViewDirection;
        if (_componentGui.ModalPanelWidget != null || DialogsManager.HasDialogs(_componentPlayer.GuiWidget))
        {
            if (!input.IsMouseCursorVisible)
            {
                var viewWidget = _componentPlayer.ViewWidget;
                var value = viewWidget.WidgetToScreen(viewWidget.ActualSize / 2f);
                input.IsMouseCursorVisible = true;
                input.MousePosition = value;
            }
        }
        else if (AllowHandleInput)
        {
            input.IsMouseCursorVisible = false;
            var zero = Vector2.Zero;
            var num = 0;
            if (Window.IsActive && Time.FrameDuration > 0f)
            {
                var mouseMovement = input.MouseMovement;
                var mouseWheelMovement = input.MouseWheelMovement;
                zero.X = 0.02f * mouseMovement.X / Time.FrameDuration / 60f;
                zero.Y = -0.02f * mouseMovement.Y / Time.FrameDuration / 60f;
                num = mouseWheelMovement / 120;
                if (mouseMovement != Point2.Zero)
                {
                    IsControlledByTouch = false;
                }
            }

            var vector = default(Vector3) + Vector3.UnitX * (input.IsKeyDown(Key.D) ? 1 : 0);
            vector += -Vector3.UnitZ * (input.IsKeyDown(Key.S) ? 1 : 0);
            vector += Vector3.UnitZ * (input.IsKeyDown(Key.W) ? 1 : 0);
            vector += -Vector3.UnitX * (input.IsKeyDown(Key.A) ? 1 : 0);
            vector += Vector3.UnitY * (input.IsKeyDown(Key.Space) ? 1 : 0);
            vector += -Vector3.UnitY * (input.IsKeyDown(Key.Shift) ? 1 : 0);
            _playerInput.Look += new Vector2(MathUtils.Clamp(zero.X, -15f, 15f), MathUtils.Clamp(zero.Y, -15f, 15f));
            _playerInput.Move += vector;
            _playerInput.SneakMove += vector;
            _playerInput.Jump |= input.IsKeyDownOnce(Key.Space);
            _playerInput.ScrollInventory -= num;
            _playerInput.Dig = input.IsMouseButtonDown(MouseButton.Left)
                ? new Ray3(viewPosition, viewDirection)
                : _playerInput.Dig;
            _playerInput.Hit = input.IsMouseButtonDownOnce(MouseButton.Left)
                ? new Ray3(viewPosition, viewDirection)
                : _playerInput.Hit;
            _playerInput.Aim = input.IsMouseButtonDown(MouseButton.Right)
                ? new Ray3(viewPosition, viewDirection)
                : _playerInput.Aim;
            _playerInput.Interact = input.IsMouseButtonDownOnce(MouseButton.Right)
                ? new Ray3(viewPosition, viewDirection)
                : _playerInput.Interact;
            _playerInput.ToggleSneak |= input.IsKeyDownOnce(Key.Shift);
            _playerInput.ToggleMount |= input.IsKeyDownOnce(Key.R);
            _playerInput.ToggleCreativeFly |= input.IsKeyDownOnce(Key.F);
            _playerInput.PickBlockType = input.IsMouseButtonDownOnce(MouseButton.Middle)
                ? new Ray3(viewPosition, viewDirection)
                : _playerInput.PickBlockType;
        }

        if (!DialogsManager.HasDialogs(_componentPlayer.GuiWidget) && AllowHandleInput)
        {
            _playerInput.ToggleInventory |= input.IsKeyDownOnce(Key.E);
            _playerInput.ToggleClothing |= input.IsKeyDownOnce(Key.C);
            _playerInput.TakeScreenshot |= input.IsKeyDownOnce(Key.P);
            _playerInput.SwitchCameraMode |= input.IsKeyDownOnce(Key.V);
            _playerInput.TimeOfDay |= input.IsKeyDownOnce(Key.T);
            _playerInput.Lighting |= input.IsKeyDownOnce(Key.L);
            _playerInput.Drop |= input.IsKeyDownOnce(Key.Q);
            _playerInput.EditItem |= input.IsKeyDownOnce(Key.G);
            _playerInput.KeyboardHelp |= input.IsKeyDownOnce(Key.H);
            _playerInput.CreateTeam |= input.IsKeyDownOnce(Key.N);
            _playerInput.JoinTeam |= input.IsKeyDownOnce(Key.J);
            _playerInput.LeaveTeam |= input.IsKeyDownOnce(Key.B);
            _playerInput.TogglePlayerShowType |= input.IsKeyDownOnce(Key.Z);
            _playerInput.Precipitation |= input.IsKeyDownOnce(Key.Y);
            _playerInput.Fog |= input.IsKeyDownOnce(Key.O);

            if (input.IsKeyDownOnce(Key.Number1))
            {
                _playerInput.SelectInventorySlot = 0;
            }

            if (input.IsKeyDownOnce(Key.Number2))
            {
                _playerInput.SelectInventorySlot = 1;
            }

            if (input.IsKeyDownOnce(Key.Number3))
            {
                _playerInput.SelectInventorySlot = 2;
            }

            if (input.IsKeyDownOnce(Key.Number4))
            {
                _playerInput.SelectInventorySlot = 3;
            }

            if (input.IsKeyDownOnce(Key.Number5))
            {
                _playerInput.SelectInventorySlot = 4;
            }

            if (input.IsKeyDownOnce(Key.Number6))
            {
                _playerInput.SelectInventorySlot = 5;
            }

            if (input.IsKeyDownOnce(Key.Number7))
            {
                _playerInput.SelectInventorySlot = 6;
            }

            if (input.IsKeyDownOnce(Key.Number8))
            {
                _playerInput.SelectInventorySlot = 7;
            }

            if (input.IsKeyDownOnce(Key.Number9))
            {
                _playerInput.SelectInventorySlot = 8;
            }

            if (input.IsKeyDownOnce(Key.Number0))
            {
                _playerInput.SelectInventorySlot = 9;
            }
        }

        ModsManager.HookAction("UpdateInput", loader =>
        {
            loader.UpdateInput(this, input);
            return false;
        });
    }

    public void UpdateInputFromGamepad(WidgetInput input)
    {
        var viewPosition = _componentPlayer.GameWidget.ActiveCamera.ViewPosition;
        var viewDirection = _componentPlayer.GameWidget.ActiveCamera.ViewDirection;
        if (_componentGui.ModalPanelWidget != null || DialogsManager.HasDialogs(_componentPlayer.GuiWidget))
        {
            if (!input.IsPadCursorVisible)
            {
                var viewWidget = _componentPlayer.ViewWidget;
                var padCursorPosition = viewWidget.WidgetToScreen(viewWidget.ActualSize / 2f);
                input.IsPadCursorVisible = true;
                input.PadCursorPosition = padCursorPosition;
            }
        }
        else
        {
            input.IsPadCursorVisible = false;
            var zero = Vector3.Zero;
            var padStickPosition = input.GetPadStickPosition(GamePadStick.Left, SettingsManager.GamepadDeadZone);
            var padStickPosition2 = input.GetPadStickPosition(GamePadStick.Right, SettingsManager.GamepadDeadZone);
            var padTriggerPosition = input.GetPadTriggerPosition(GamePadTrigger.Left);
            var padTriggerPosition2 = input.GetPadTriggerPosition(GamePadTrigger.Right);
            zero += new Vector3(2f * padStickPosition.X, 0f, 2f * padStickPosition.Y);
            zero += Vector3.UnitY * (input.IsPadButtonDown(GamePadButton.A) ? 1 : 0);
            zero += -Vector3.UnitY * (input.IsPadButtonDown(GamePadButton.RightShoulder) ? 1 : 0);
            _playerInput.Move += zero;
            _playerInput.SneakMove += zero;
            _playerInput.Look += 0.75f * padStickPosition2 * MathUtils.Pow(padStickPosition2.LengthSquared(), 0.25f);
            _playerInput.Jump |= input.IsPadButtonDownOnce(GamePadButton.A);
            _playerInput.Dig = padTriggerPosition2 >= 0.5f ? new Ray3(viewPosition, viewDirection) : _playerInput.Dig;
            _playerInput.Hit = padTriggerPosition2 >= 0.5f && _lastRightTrigger < 0.5f
                ? new Ray3(viewPosition, viewDirection)
                : _playerInput.Hit;
            _playerInput.Aim = padTriggerPosition >= 0.5f ? new Ray3(viewPosition, viewDirection) : _playerInput.Aim;
            _playerInput.Interact = padTriggerPosition >= 0.5f && _lastLeftTrigger < 0.5f
                ? new Ray3(viewPosition, viewDirection)
                : _playerInput.Interact;
            _playerInput.Drop |= input.IsPadButtonDownOnce(GamePadButton.B);
            _playerInput.ToggleMount |= input.IsPadButtonDownOnce(GamePadButton.LeftThumb) ||
                                        input.IsPadButtonDownOnce(GamePadButton.DPadUp);
            _playerInput.EditItem |= input.IsPadButtonDownOnce(GamePadButton.LeftShoulder);
            _playerInput.ToggleSneak |= input.IsPadButtonDownOnce(GamePadButton.RightShoulder);
            _playerInput.SwitchCameraMode |= input.IsPadButtonDownOnce(GamePadButton.RightThumb) ||
                                             input.IsPadButtonDownOnce(GamePadButton.DPadDown);
            if (input.IsPadButtonDownRepeat(GamePadButton.DPadLeft))
            {
                _playerInput.ScrollInventory--;
            }

            if (input.IsPadButtonDownRepeat(GamePadButton.DPadRight))
            {
                _playerInput.ScrollInventory++;
            }

            if (padStickPosition != Vector2.Zero || padStickPosition2 != Vector2.Zero)
            {
                IsControlledByTouch = false;
            }

            _lastLeftTrigger = padTriggerPosition;
            _lastRightTrigger = padTriggerPosition2;
        }

        if (!DialogsManager.HasDialogs(_componentPlayer.GuiWidget) && AllowHandleInput)
        {
            _playerInput.ToggleInventory |= input.IsPadButtonDownOnce(GamePadButton.X);
            _playerInput.ToggleClothing |= input.IsPadButtonDownOnce(GamePadButton.Y);
            _playerInput.GamepadHelp |= input.IsPadButtonDownOnce(GamePadButton.Start);
        }
    }

    public void UpdateInputFromWidgets(WidgetInput input)
    {
        var num = MathUtils.Pow(1.25f, 10f * (SettingsManager.MoveSensitivity - 0.5f));
        var num2 = MathUtils.Pow(1.25f, 10f * (SettingsManager.LookSensitivity - 0.5f));
        var num3 = MathUtils.Clamp(_subsystemTime.GameTimeDelta, 0f, 0.1f);
        var viewWidget = _componentPlayer.ViewWidget;
        _componentGui.MoveWidget.Radius = 30f / num * _componentGui.MoveWidget.GlobalScale;
        if (_componentGui.ModalPanelWidget != null || !(_subsystemTime.GameTimeFactor > 0f) || !(num3 > 0f))
        {
            return;
        }

        var v = new Vector2(SettingsManager.LeftHandedLayout ? 96 : -96, -96f);
        if (input.Widget != null)
        {
            v = Vector2.TransformNormal(v, input.Widget.GlobalTransform);
        }

        if (_componentGui.ViewWidget is { TouchInput: not null })
        {
            IsControlledByTouch = true;
            var value = _componentGui.ViewWidget.TouchInput.Value;
            var activeCamera = _componentPlayer.GameWidget.ActiveCamera;
            var viewPosition = activeCamera.ViewPosition;
            var viewDirection = activeCamera.ViewDirection;
            var direction =
                Vector3.Normalize(activeCamera.ScreenToWorld(new Vector3(value.Position, 1f), Matrix.Identity) -
                                  viewPosition);
            var direction2 =
                Vector3.Normalize(activeCamera.ScreenToWorld(new Vector3(value.Position + v, 1f), Matrix.Identity) -
                                  viewPosition);
            if (value.InputType == TouchInputType.Tap)
            {
                if (SettingsManager.LookControlMode == LookControlMode.SplitTouch)
                {
                    _playerInput.Interact = new Ray3(viewPosition, viewDirection);
                    _playerInput.Hit = new Ray3(viewPosition, viewDirection);
                }
                else
                {
                    _playerInput.Interact = new Ray3(viewPosition, direction);
                    _playerInput.Hit = new Ray3(viewPosition, direction);
                }
            }
            else if (value is { InputType: TouchInputType.Hold, DurationFrames: > 1, Duration: > 0.2f })
            {
                _playerInput.Dig = SettingsManager.LookControlMode == LookControlMode.SplitTouch
                    ? new Ray3(viewPosition, viewDirection)
                    : new Ray3(viewPosition, direction);
                _playerInput.Aim = new Ray3(viewPosition, direction2);

                _isViewHoldStarted = true;
            }
            else if (value.InputType == TouchInputType.Move)
            {
                if (SettingsManager.LookControlMode == LookControlMode.EntireScreen ||
                    SettingsManager.LookControlMode == LookControlMode.SplitTouch)
                {
                    var v2 = Vector2.TransformNormal(value.Move, _componentGui.ViewWidget.InvertedGlobalTransform);
                    var vector = num2 / num3 * new Vector2(0.0006f, -0.0006f) * v2 *
                                 MathUtils.Pow(v2.LengthSquared(), 0.125f);
                    _playerInput.Look += vector;
                }

                if (_isViewHoldStarted)
                {
                    _playerInput.Dig = SettingsManager.LookControlMode == LookControlMode.SplitTouch
                        ? new Ray3(viewPosition, viewDirection)
                        : new Ray3(viewPosition, direction);
                    _playerInput.Aim = new Ray3(viewPosition, direction2);
                }
            }
        }
        else
        {
            _isViewHoldStarted = false;
        }

        if (_componentGui.MoveWidget is { TouchInput: not null })
        {
            IsControlledByTouch = true;
            var radius = _componentGui.MoveWidget.Radius;
            var value2 = _componentGui.MoveWidget.TouchInput.Value;
            if (value2.InputType == TouchInputType.Tap)
            {
                _playerInput.Jump = true;
            }
            else if (value2.InputType is TouchInputType.Move or TouchInputType.Hold)
            {
                var v3 = Vector2.TransformNormal(value2.Move, _componentGui.ViewWidget.InvertedGlobalTransform);
                var vector2 = num / num3 * new Vector2(0.003f, -0.003f) * v3 *
                              MathUtils.Pow(v3.LengthSquared(), 0.175f);
                _playerInput.SneakMove.X += vector2.X;
                _playerInput.SneakMove.Z += vector2.Y;
                var vector3 = Vector2.TransformNormal(value2.TotalMoveLimited,
                    _componentGui.ViewWidget.InvertedGlobalTransform);
                _playerInput.Move.X += ProcessInputValue(vector3.X * viewWidget.GlobalScale, 0.2f * radius, radius);
                _playerInput.Move.Z +=
                    ProcessInputValue((0f - vector3.Y) * viewWidget.GlobalScale, 0.2f * radius, radius);
            }
        }

        if (_componentGui.MoveRoseWidget.Direction != Vector3.Zero || _componentGui.MoveRoseWidget.Jump)
        {
            IsControlledByTouch = true;
        }

        _playerInput.Move += _componentGui.MoveRoseWidget.Direction;
        _playerInput.SneakMove += _componentGui.MoveRoseWidget.Direction;
        _playerInput.Jump |= _componentGui.MoveRoseWidget.Jump;

        if (_componentGui.LookWidget is not { TouchInput: not null })
        {
            return;
        }

        IsControlledByTouch = true;
        var value3 = _componentGui.LookWidget.TouchInput.Value;
        if (value3.InputType == TouchInputType.Tap)
        {
            _playerInput.Jump = true;
        }
        else if (value3.InputType == TouchInputType.Move)
        {
            var v4 = Vector2.TransformNormal(value3.Move, _componentGui.ViewWidget.InvertedGlobalTransform);
            var vector4 = num2 / num3 * new Vector2(0.0006f, -0.0006f) * v4 *
                          MathUtils.Pow(v4.LengthSquared(), 0.125f);
            _playerInput.Look += vector4;
        }
    }

    public static float ProcessInputValue(float value, float deadZone, float saturationZone)
    {
        return MathUtils.Sign(value) *
               MathUtils.Clamp((MathUtils.Abs(value) - deadZone) / (saturationZone - deadZone), 0f, 1f);
    }
}

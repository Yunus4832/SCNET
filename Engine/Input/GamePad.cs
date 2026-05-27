#if ANDROID
using System.Collections.Concurrent;

using Android.Views;

using Axis = Android.Views.Axis;
#endif
#if DESKTOP
using Silk.NET.Input;
#endif

using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static class GamePad
{
    internal class State
    {
        public bool IsConnected;

        public Vector2[] Sticks = new Vector2[2];

        public float[] Triggers = new float[2];

        public float[] LastTriggers = new float[2];

        public bool[] Buttons = new bool[14];

        public bool[] LastButtons = new bool[14];

        public double[] ButtonsRepeat = new double[14];

        public object? ModifierKeyOfCurrentCombo;
    }

#if ANDROID
    public struct KeyInfo(int gamepadIndex, GamePadButton button, bool press)
    {
        public int GamepadIndex = gamepadIndex;
        public GamePadButton Button = button;
        public bool Press = press;
    }

    public struct TriggerInfo(int gamepadIndex, GamePadTrigger trigger, float value)
    {
        public int GamepadIndex = gamepadIndex;
        public GamePadTrigger Trigger = trigger;
        public float Value = value;
    }

    public static Dictionary<int, int> DeviceToIndex = [];
    public static List<int> DeviceToRemove = [];
    public static ConcurrentQueue<KeyInfo> CachedKeyEvents = [];
    public static ConcurrentQueue<TriggerInfo> CachedTriggerEvents = [];
    public static readonly bool[,] LastDpadStates = new bool[4, 4];
    public static readonly bool[,] DpadFromKey = new bool[4, 4];
    public static readonly bool[,] LastTriggerDown = new bool[4, 2];

    private const float _triggerDownThreshold = 0.5f;
    private const float _triggerUpThreshold = 0.4f;

#endif

#if DESKTOP
    public static IReadOnlyList<IGamepad> Gamepads = null!;
#endif

    public static double ButtonFirstRepeatTime = 0.2;

    public static double ButtonNextRepeatTime = 0.04;

    internal static State[] states = [new(), new(), new(), new()];

    internal static void Initialize()
    {
#if DESKTOP
        if (Window.InputContext is null)
        {
            throw new InvalidOperationException("Window.InputContext is not set");
        }

        Gamepads = Window.InputContext.Gamepads;
#endif
    }

    internal static void Dispose()
    {
    }

#if ANDROID
    internal static void BeforeFrame()
    {
        if (Time.PeriodicEvent(2.0, 0.0))
        {
            DeviceToRemove.Clear();
            foreach (var key in DeviceToIndex.Keys)
            {
                if (InputDevice.GetDevice(key) is null)
                {
                    DeviceToRemove.Add(key);
                }
            }

            foreach (var item in DeviceToRemove)
            {
                Disconnect(item);
            }
        }

        while (CachedKeyEvents.TryDequeue(out var keyInfo))
        {
            if (keyInfo.Press)
            {
                HandleKeyDown(keyInfo.GamepadIndex, keyInfo.Button);
            }
            else
            {
                HandleKeyUp(keyInfo.GamepadIndex, keyInfo.Button);
            }
        }

        while (CachedTriggerEvents.TryDequeue(out var info))
        {
            var num = TranslateDeviceId(info.GamepadIndex);
            if (num >= 0)
            {
                // 在这里更新 Triggers，此时它是当前帧的最新值
                states[num].Triggers[(int)info.Trigger] = info.Value;
            }
        }
    }

    public static void HandleKeyEvent(KeyEvent e)
    {
        CachedKeyEvents.Enqueue(new KeyInfo(TranslateDeviceId(e.DeviceId), TranslateKey(e.KeyCode),
            e.Action == KeyEventActions.Down));
    }

    internal static void HandleKeyDown(int gamepadIndex, GamePadButton gamePadButton)
    {
        if (gamepadIndex < 0)
        {
            return;
        }

        switch (gamePadButton)
        {
            case < GamePadButton.A:
                return;
            case >= GamePadButton.DPadLeft and <= GamePadButton.DPadDown:
            {
                var idx = gamePadButton switch
                {
                    GamePadButton.DPadLeft => 0,
                    GamePadButton.DPadRight => 1,
                    GamePadButton.DPadUp => 2,
                    GamePadButton.DPadDown => 3,
                    _ => throw new ArgumentOutOfRangeException(nameof(gamePadButton))
                };
                DpadFromKey[gamepadIndex, idx] = true;
                break;
            }
            case GamePadButton.LeftShoulder: states[gamepadIndex].Triggers[0] = 1f; break;
            case GamePadButton.RightShoulder: states[gamepadIndex].Triggers[1] = 1f; break;
        }

        states[gamepadIndex].Buttons[(int)gamePadButton] = true;
    }

    internal static void HandleKeyUp(int gamepadIndex, GamePadButton gamePadButton)
    {
        if (gamepadIndex < 0)
        {
            return;
        }

        switch (gamePadButton)
        {
            case < GamePadButton.A:
                return;
            case >= GamePadButton.DPadLeft and <= GamePadButton.DPadDown:
            {
                var idx = gamePadButton switch
                {
                    GamePadButton.DPadLeft => 0,
                    GamePadButton.DPadRight => 1,
                    GamePadButton.DPadUp => 2,
                    GamePadButton.DPadDown => 3,
                    _ => throw new ArgumentOutOfRangeException(nameof(gamePadButton))
                };
                DpadFromKey[gamepadIndex, idx] = false;
                break;
            }
            case GamePadButton.LeftShoulder: states[gamepadIndex].Triggers[0] = 0f; break;
            case GamePadButton.RightShoulder: states[gamepadIndex].Triggers[1] = 0f; break;
        }

        states[gamepadIndex].Buttons[(int)gamePadButton] = false;
    }

    internal static void HandleMotionEvent(MotionEvent e)
    {
        var gamepadIndex = TranslateDeviceId(e.DeviceId);
        if (gamepadIndex < 0)
        {
            return;
        }

        states[gamepadIndex].Sticks[0] = new Vector2(e.GetAxisValue(Axis.X), 0f - e.GetAxisValue(Axis.Y));
        states[gamepadIndex].Sticks[1] = new Vector2(e.GetAxisValue(Axis.Z), 0f - e.GetAxisValue(Axis.Rz));
        var l = MathF.Max(e.GetAxisValue(Axis.Ltrigger), e.GetAxisValue(Axis.Brake));
        var r = MathF.Max(e.GetAxisValue(Axis.Rtrigger), e.GetAxisValue(Axis.Gas));
        CachedTriggerEvents.Enqueue(new TriggerInfo(TranslateDeviceId(e.DeviceId), GamePadTrigger.Left, l));
        CachedTriggerEvents.Enqueue(new TriggerInfo(TranslateDeviceId(e.DeviceId), GamePadTrigger.Right, r));
        var axisX = e.GetAxisValue(Axis.HatX);
        var axisY = e.GetAxisValue(Axis.HatY);
        ProcessDpad(gamepadIndex, gamepadIndex, 0, axisX < -0.5f, GamePadButton.DPadLeft);
        ProcessDpad(gamepadIndex, gamepadIndex, 1, axisX > 0.5f, GamePadButton.DPadRight);
        ProcessDpad(gamepadIndex, gamepadIndex, 2, axisY < -0.5f, GamePadButton.DPadUp);
        ProcessDpad(gamepadIndex, gamepadIndex, 3, axisY > 0.5f, GamePadButton.DPadDown);
    }

    public static void ProcessDpad(int gamepadIndex, int padIndex, int dpadIndex, bool current, GamePadButton button)
    {
        if (DpadFromKey[padIndex, dpadIndex])
        {
            return;
        }

        var last = LastDpadStates[padIndex, dpadIndex];
        if (current == last)
        {
            return;
        }

        LastDpadStates[padIndex, dpadIndex] = current;
        CachedKeyEvents.Enqueue(new KeyInfo(gamepadIndex, button, current));
    }

    public static int TranslateDeviceId(int deviceId)
    {
        if (DeviceToIndex.TryGetValue(deviceId, out var value))
        {
            return value;
        }

        for (var i = 0; i < 4; i++)
        {
            if (DeviceToIndex.Values.Contains(i))
            {
                continue;
            }

            Connect(deviceId, i);
            return i;
        }

        return -1;
    }

    public static GamePadButton TranslateKey(Keycode keyCode) => keyCode switch
    {
        Keycode.ButtonA => GamePadButton.A,
        Keycode.ButtonB => GamePadButton.B,
        Keycode.ButtonX => GamePadButton.X,
        Keycode.ButtonY => GamePadButton.Y,
        Keycode.Back => GamePadButton.Back,
        Keycode.ButtonL1 => GamePadButton.LeftShoulder,
        Keycode.ButtonR1 => GamePadButton.RightShoulder,
        Keycode.ButtonThumbl => GamePadButton.LeftThumb,
        Keycode.ButtonThumbr => GamePadButton.RightThumb,
        Keycode.DpadLeft => GamePadButton.DPadLeft,
        Keycode.DpadRight => GamePadButton.DPadRight,
        Keycode.DpadUp => GamePadButton.DPadUp,
        Keycode.DpadDown => GamePadButton.DPadDown,
        Keycode.ButtonSelect => GamePadButton.Back,
        Keycode.ButtonStart => GamePadButton.Start,
        _ => (GamePadButton)(-1)
    };

    public static void Connect(int deviceId, int index)
    {
        DeviceToIndex.Add(deviceId, index);
        states[index].IsConnected = true;
    }

    public static void Disconnect(int deviceId)
    {
        if (DeviceToIndex.Remove(deviceId, out var value))
        {
            states[value].IsConnected = false;
        }
    }
#endif
#if DESKTOP
    internal static void BeforeFrame()
    {
        for (var padIndex = 0; padIndex < 4; padIndex++)
        {
            if (padIndex >= Gamepads.Count)
            {
                break;
            }

            var gamepad = Gamepads[padIndex];
            if (gamepad is null)
            {
                continue;
            }

            var name = gamepad.Name;
            if (name.Contains("Unmapped"))
            {
                continue;
            }

            var state = states[padIndex];
            if (gamepad.IsConnected)
            {
                state.IsConnected = true;
                if (!Window.IsActive)
                {
                    continue;
                }

                var thumbsticks = gamepad.Thumbsticks;
                for (var i = 0; i < 2; i++)
                {
                    state.Sticks[i] = new Vector2(thumbsticks[i].X, -thumbsticks[i].Y);
                }

                var triggers = gamepad.Triggers;
                for (var i = 0; i < 2; i++)
                {
                    state.Triggers[i] = triggers[i].Position;
                }

                foreach (var button in gamepad.Buttons)
                {
                    switch (button.Name)
                    {
                        case ButtonName.A: state.Buttons[0] = button.Pressed; break;
                        case ButtonName.B: state.Buttons[1] = button.Pressed; break;
                        case ButtonName.X: state.Buttons[2] = button.Pressed; break;
                        case ButtonName.Y: state.Buttons[3] = button.Pressed; break;
                        case ButtonName.Back: state.Buttons[4] = button.Pressed; break;
                        case ButtonName.Start: state.Buttons[5] = button.Pressed; break;
                        case ButtonName.LeftStick: state.Buttons[6] = button.Pressed; break;
                        case ButtonName.RightStick: state.Buttons[7] = button.Pressed; break;
                        case ButtonName.LeftBumper: state.Buttons[8] = button.Pressed; break;
                        case ButtonName.RightBumper: state.Buttons[9] = button.Pressed; break;
                        case ButtonName.DPadLeft: state.Buttons[10] = button.Pressed; break;
                        case ButtonName.DPadRight: state.Buttons[12] = button.Pressed; break;
                        case ButtonName.DPadUp: state.Buttons[11] = button.Pressed; break;
                        case ButtonName.DPadDown: state.Buttons[13] = button.Pressed; break;
                    }
                }
            }
            else
            {
                state.IsConnected = false;
            }
        }
    }
#endif

    public static bool IsConnected(int gamePadIndex) => gamePadIndex < 0 || gamePadIndex >= states.Length
        ? throw new ArgumentOutOfRangeException(nameof(gamePadIndex))
        : states[gamePadIndex].IsConnected;

    public static Vector2 GetStickPosition(int gamePadIndex, GamePadStick stick, float deadZone = 0f)
    {
        if (deadZone is < 0f or >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZone));
        }

        if (!IsConnected(gamePadIndex))
        {
            return Vector2.Zero;
        }

        var result = states[gamePadIndex].Sticks[(int)stick];
        if (!(deadZone > 0f))
        {
            return result;
        }

        var num = result.Length();
        if (!(num > 0f))
        {
            return result;
        }

        var num2 = ApplyDeadZone(num, deadZone);
        result *= num2 / num;

        return result;
    }

    public static float GetTriggerPosition(int gamePadIndex, GamePadTrigger trigger, float deadZone = 0f) =>
        deadZone < 0f || deadZone >= 1f ? throw new ArgumentOutOfRangeException(nameof(deadZone)) :
        IsConnected(gamePadIndex) ? ApplyDeadZone(states[gamePadIndex].Triggers[(int)trigger], deadZone) : 0f;

    public static bool IsTriggerDown(int gamePadIndex, GamePadTrigger trigger, float deadZone = 0f,
        float threshold = 0.5f)
    {
        if (deadZone is < 0f or >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZone));
        }

        if (!IsConnected(gamePadIndex))
        {
            return false;
        }

        var value = ApplyDeadZone(states[gamePadIndex].Triggers[(int)trigger], deadZone);
        return value >= threshold;
    }

    public static bool IsTriggerDownOnce(int gamePadIndex, GamePadTrigger trigger, float deadZone = 0f,
        float threshold = 0.5f)
    {
        if (deadZone is < 0f or >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZone));
        }

        if (!IsConnected(gamePadIndex))
        {
            return false;
        }

        if (states[gamePadIndex].ModifierKeyOfCurrentCombo is GamePadTrigger trigger1 &&
            trigger1 == trigger)
        {
            return false; //若修饰键按下期间已触发组合键，禁止当前修饰键触发自己的点按行为，避免误触
        }

        var current = ApplyDeadZone(states[gamePadIndex].Triggers[(int)trigger], deadZone) >= threshold;
        var last = ApplyDeadZone(states[gamePadIndex].LastTriggers[(int)trigger], deadZone) >= threshold;
        return !current && last; //扳机必定是修饰键，松开那一刻才算按下一次，避免影响组合键
    }

    public static bool IsButtonDown(int gamePadIndex, GamePadButton button) =>
        IsConnected(gamePadIndex) && states[gamePadIndex].Buttons[(int)button];

    public static bool IsButtonDownOnce(int gamePadIndex, GamePadButton button)
    {
        if (!IsConnected(gamePadIndex))
        {
            return false;
        }

        if (states[gamePadIndex].ModifierKeyOfCurrentCombo is GamePadButton button1 &&
            button1 == button)
        {
            return false;
        }

        if (IsModifierKey(button))
            //如果是修饰键，松开那一刻才算按下一次，避免影响组合键
        {
            return !states[gamePadIndex].Buttons[(int)button] && states[gamePadIndex].LastButtons[(int)button];
        }

        //正常按键依然是按下那一刻算按下一次
        return states[gamePadIndex].Buttons[(int)button] && !states[gamePadIndex].LastButtons[(int)button];
    }

    public static bool IsButtonDownRepeat(int gamePadIndex, GamePadButton button)
    {
        if (!IsConnected(gamePadIndex))
        {
            return false;
        }

        if (states[gamePadIndex].Buttons[(int)button]
            && !states[gamePadIndex].LastButtons[(int)button])
        {
            return true;
        }

        var num = states[gamePadIndex].ButtonsRepeat[(int)button];
        return num != 0.0 && Time.FrameStartTime >= num;
    }

    public static bool IsAnyModifierKeyHolding(int gamePadIndex, float threshold = 0.5f)
    {
        if (!IsConnected(gamePadIndex))
        {
            return false;
        }

        var state = states[gamePadIndex];
        return state.Triggers[0] >= threshold ||
               state.Triggers[1] >= threshold ||
               state.Buttons[(int)GamePadButton.LeftShoulder] ||
               state.Buttons[(int)GamePadButton.RightShoulder];
    }

    public static void SetModifierKeyOfCurrentCombo(int gamePadIndex, object modifierKey)
    {
        if (!IsConnected(gamePadIndex))
        {
            return;
        }

        if (IsModifierKey(modifierKey))
        {
            states[gamePadIndex].ModifierKeyOfCurrentCombo = modifierKey;
        }
    }

    public static bool IsModifierKey(object obj) => obj is GamePadTrigger
        or GamePadButton and (GamePadButton.LeftShoulder or GamePadButton.RightShoulder);

    public static void Clear()
    {
        foreach (var state in states)
        {
            for (var j = 0; j < state.Sticks.Length; j++)
            {
                state.Sticks[j] = Vector2.Zero;
            }

            for (var k = 0; k < state.Triggers.Length; k++)
            {
                state.Triggers[k] = 0f;
            }

            for (var l = 0; l < state.Buttons.Length; l++)
            {
                state.Buttons[l] = false;
                state.ButtonsRepeat[l] = 0.0;
            }
        }
    }

    internal static void AfterFrame()
    {
        for (var i = 0; i < states.Length; i++)
        {
            if (Keyboard.BackButtonQuitsApp
                && IsButtonDownOnce(i, GamePadButton.Back))
            {
                Window.Close();
            }

            var state = states[i];
            for (var j = 0; j < state.Buttons.Length; j++)
            {
                if (state.Buttons[j])
                {
                    if (!state.LastButtons[j])
                    {
                        state.ButtonsRepeat[j] = Time.FrameStartTime + ButtonFirstRepeatTime;
                    }
                    else if (Time.FrameStartTime >= state.ButtonsRepeat[j])
                    {
                        state.ButtonsRepeat[j] = Math.Max(Time.FrameStartTime,
                            state.ButtonsRepeat[j] + ButtonNextRepeatTime);
                    }
                }
                else
                {
                    state.ButtonsRepeat[j] = 0.0;
                }

                state.LastButtons[j] = state.Buttons[j];
            }

            for (var k = 0; k < state.Triggers.Length; k++)
            {
                state.LastTriggers[k] = state.Triggers[k];
            }

            if (!IsAnyModifierKeyHolding(i, 0.08f))
            {
                state.ModifierKeyOfCurrentCombo = null!;
            }
        }
    }

    public static float ApplyDeadZone(float value, float deadZone) =>
        MathF.Sign(value) * MathF.Max(MathF.Abs(value) - deadZone, 0f) / (1f - deadZone);
}

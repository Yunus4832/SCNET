using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class GamePad
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

    public static double ButtonFirstRepeatTime = 0.2;

    public static double ButtonNextRepeatTime = 0.04;

    internal static State[] states = [new(), new(), new(), new()];

    internal static void Initialize()
    {
        InitializePlatform();
    }

    internal static void Dispose()
    {
    }

    internal static void BeforeFrame()
    {
        BeforeFramePlatform();
    }

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
        deadZone is < 0f or >= 1f ? throw new ArgumentOutOfRangeException(nameof(deadZone)) :
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

    static partial void InitializePlatform();

    static partial void BeforeFramePlatform();
}

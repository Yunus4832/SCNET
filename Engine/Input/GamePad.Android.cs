#if ANDROID
using System.Collections.Concurrent;

using Android.Views;

using Engine.Core;

using Axis = Android.Views.Axis;

namespace Engine.Input;

public static partial class GamePad
{
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

    static partial void BeforeFramePlatform()
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
}
#endif

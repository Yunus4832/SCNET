#if DESKTOP
using Silk.NET.Input;

using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class GamePad
{
    public static IReadOnlyList<IGamepad> Gamepads = null!;

    static partial void InitializePlatform()
    {
        if (Window.InputContext is null)
        {
            throw new InvalidOperationException("Window.InputContext is not set");
        }

        Gamepads = Window.InputContext.Gamepads;
    }

    static partial void BeforeFramePlatform()
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
}
#endif

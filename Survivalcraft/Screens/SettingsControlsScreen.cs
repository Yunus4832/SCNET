using System.Globalization;
using System.Xml.Linq;

using Game.Controls;

namespace Game.Screens;

public class SettingsControlsScreen : Screen
{
    private const string _typeName = nameof(SettingsControlsScreen);

    private readonly ButtonWidget _autoJumpButton;

    private readonly SliderWidget _dragDistanceSlider;

    private readonly ButtonWidget _flipVerticalAxisButton;

    private readonly SliderWidget _gamepadCursorSpeedSlider;

    private readonly SliderWidget _gamepadDeadZoneSlider;

    private readonly SliderWidget _holdDurationSlider;

    private readonly ButtonWidget _leftHandedLayoutButton;

    private readonly ButtonWidget _lookControlModeButton;

    private readonly SliderWidget _lookSensitivitySlider;

    private readonly ButtonWidget _moveControlModeButton;

    private readonly SliderWidget _moveSensitivitySlider;

    public SettingsControlsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsControlsScreen");
        LoadContents(this, node);
        _moveControlModeButton = Children.Find<ButtonWidget>("MoveControlMode")!;
        _lookControlModeButton = Children.Find<ButtonWidget>("LookControlMode")!;
        _leftHandedLayoutButton = Children.Find<ButtonWidget>("LeftHandedLayout")!;
        _flipVerticalAxisButton = Children.Find<ButtonWidget>("FlipVerticalAxis")!;
        _autoJumpButton = Children.Find<ButtonWidget>("AutoJump")!;
        _moveSensitivitySlider = Children.Find<SliderWidget>("MoveSensitivitySlider")!;
        _lookSensitivitySlider = Children.Find<SliderWidget>("LookSensitivitySlider")!;
        _gamepadCursorSpeedSlider = Children.Find<SliderWidget>("GamepadCursorSpeedSlider")!;
        _gamepadDeadZoneSlider = Children.Find<SliderWidget>("GamepadDeadZoneSlider")!;
        _holdDurationSlider = Children.Find<SliderWidget>("HoldDurationSlider")!;
        _dragDistanceSlider = Children.Find<SliderWidget>("DragDistanceSlider")!;
    }

    public override void Update()
    {
        if (_moveControlModeButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.MoveControlMode =
                (MoveControlMode)((int)(Configuration.ClientConfiguration.Preferences.MoveControlMode + 1) %
                                  EnumUtils.GetEnumValues(typeof(MoveControlMode)).Count);
        }

        if (_lookControlModeButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.LookControlMode =
                (LookControlMode)((int)(Configuration.ClientConfiguration.Preferences.LookControlMode + 1) %
                                  EnumUtils.GetEnumValues(typeof(LookControlMode)).Count);
        }

        if (_leftHandedLayoutButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.LeftHandedLayout =
                !Configuration.ClientConfiguration.Preferences.LeftHandedLayout;
        }

        if (_flipVerticalAxisButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.FlipVerticalAxis =
                !Configuration.ClientConfiguration.Preferences.FlipVerticalAxis;
        }

        if (_autoJumpButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.AutoJump =
                !Configuration.ClientConfiguration.Preferences.AutoJump;
        }

        if (_moveSensitivitySlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.MoveSensitivity = _moveSensitivitySlider.Value;
        }

        if (_lookSensitivitySlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.LookSensitivity = _lookSensitivitySlider.Value;
        }

        if (_gamepadCursorSpeedSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.GamepadCursorSpeed = _gamepadCursorSpeedSlider.Value;
        }

        if (_gamepadDeadZoneSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.GamepadDeadZone = _gamepadDeadZoneSlider.Value;
        }

        if (_holdDurationSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.MinimumHoldDuration = _holdDurationSlider.Value;
        }

        if (_dragDistanceSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.MinimumDragDistance = _dragDistanceSlider.Value;
        }

        _moveControlModeButton.Text =
            Localization.ClientLanguage.Catalog.Get(
                "MoveControlMode",
                Configuration.ClientConfiguration.Preferences.MoveControlMode.ToString()
            );
        _lookControlModeButton.Text =
            Localization.ClientLanguage.Catalog.Get(
                "LookControlMode",
                Configuration.ClientConfiguration.Preferences.LookControlMode.ToString()
            );
        _leftHandedLayoutButton.Text = Configuration.ClientConfiguration.Preferences.LeftHandedLayout
            ? Localization.ClientLanguage.Catalog.Get("Usual", "on")
            : Localization.ClientLanguage.Catalog.Get("Usual", "off");
        _flipVerticalAxisButton.Text = Configuration.ClientConfiguration.Preferences.FlipVerticalAxis
            ? Localization.ClientLanguage.Catalog.Get("Usual", "on")
            : Localization.ClientLanguage.Catalog.Get("Usual", "off");
        _autoJumpButton.Text = Configuration.ClientConfiguration.Preferences.AutoJump
            ? Localization.ClientLanguage.Catalog.Get("Usual", "on")
            : Localization.ClientLanguage.Catalog.Get("Usual", "off");
        _moveSensitivitySlider.Value = Configuration.ClientConfiguration.Preferences.MoveSensitivity;
        _moveSensitivitySlider.Text = MathUtils
            .Round(Configuration.ClientConfiguration.Preferences.MoveSensitivity * 10f)
            .ToString(CultureInfo.InvariantCulture);
        _lookSensitivitySlider.Value = Configuration.ClientConfiguration.Preferences.LookSensitivity;
        _lookSensitivitySlider.Text = MathUtils
            .Round(Configuration.ClientConfiguration.Preferences.LookSensitivity * 10f)
            .ToString(CultureInfo.InvariantCulture);
        _gamepadCursorSpeedSlider.Value = Configuration.ClientConfiguration.Preferences.GamepadCursorSpeed;
        _gamepadCursorSpeedSlider.Text =
            $"{Configuration.ClientConfiguration.Preferences.GamepadCursorSpeed:0.0}x";
        _gamepadDeadZoneSlider.Value = Configuration.ClientConfiguration.Preferences.GamepadDeadZone;
        _gamepadDeadZoneSlider.Text = $"{Configuration.ClientConfiguration.Preferences.GamepadDeadZone * 100f:0}%";
        _holdDurationSlider.Value = Configuration.ClientConfiguration.Preferences.MinimumHoldDuration;
        _holdDurationSlider.Text =
            $"{MathUtils.Round(1000f * Configuration.ClientConfiguration.Preferences.MinimumHoldDuration)}ms";
        _dragDistanceSlider.Value = Configuration.ClientConfiguration.Preferences.MinimumDragDistance;
        _dragDistanceSlider.Text =
            $"{MathUtils.Round(Configuration.ClientConfiguration.Preferences.MinimumDragDistance)} " +
            Localization.ClientLanguage.Catalog.Get(_typeName, 2);
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}

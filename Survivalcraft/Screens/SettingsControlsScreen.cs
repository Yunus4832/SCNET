using System.Globalization;
using System.Xml.Linq;

namespace Game.Screens;

public class SettingsControlsScreen : Screen
{
    private const string _typeName = "SettingsControlsScreen";

    private readonly ButtonWidget _autoJumpButton;

    private readonly SliderWidget _creativeDigTimeSlider;

    private readonly SliderWidget _creativeReachSlider;

    private readonly SliderWidget _dragDistanceSlider;

    private readonly ButtonWidget _flipVerticalAxisButton;

    private readonly SliderWidget _gamepadCursorSpeedSlider;

    private readonly SliderWidget _gamepadDeadZoneSlider;

    private readonly SliderWidget _holdDurationSlider;

    private readonly ButtonWidget _horizontalCreativeFlightButton;

    private readonly ContainerWidget _horizontalCreativeFlightPanel;

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
        _horizontalCreativeFlightButton = Children.Find<ButtonWidget>("HorizontalCreativeFlight")!;
        _horizontalCreativeFlightPanel = Children.Find<ContainerWidget>("HorizontalCreativeFlightPanel")!;
        _moveSensitivitySlider = Children.Find<SliderWidget>("MoveSensitivitySlider")!;
        _lookSensitivitySlider = Children.Find<SliderWidget>("LookSensitivitySlider")!;
        _gamepadCursorSpeedSlider = Children.Find<SliderWidget>("GamepadCursorSpeedSlider")!;
        _gamepadDeadZoneSlider = Children.Find<SliderWidget>("GamepadDeadZoneSlider")!;
        _creativeDigTimeSlider = Children.Find<SliderWidget>("CreativeDigTimeSlider")!;
        _creativeReachSlider = Children.Find<SliderWidget>("CreativeReachSlider")!;
        _holdDurationSlider = Children.Find<SliderWidget>("HoldDurationSlider")!;
        _dragDistanceSlider = Children.Find<SliderWidget>("DragDistanceSlider")!;
        _horizontalCreativeFlightPanel.IsVisible = false;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_moveControlModeButton.IsClicked)
        {
            SettingsManager.MoveControlMode = (MoveControlMode)((int)(SettingsManager.MoveControlMode + 1) %
                                                                EnumUtils.GetEnumValues(typeof(MoveControlMode)).Count);
        }

        if (_lookControlModeButton.IsClicked)
        {
            SettingsManager.LookControlMode = (LookControlMode)((int)(SettingsManager.LookControlMode + 1) %
                                                                EnumUtils.GetEnumValues(typeof(LookControlMode)).Count);
        }

        if (_leftHandedLayoutButton.IsClicked)
        {
            SettingsManager.LeftHandedLayout = !SettingsManager.LeftHandedLayout;
        }

        if (_flipVerticalAxisButton.IsClicked)
        {
            SettingsManager.FlipVerticalAxis = !SettingsManager.FlipVerticalAxis;
        }

        if (_autoJumpButton.IsClicked)
        {
            SettingsManager.AutoJump = !SettingsManager.AutoJump;
        }

        if (_horizontalCreativeFlightButton.IsClicked)
        {
            SettingsManager.HorizontalCreativeFlight = !SettingsManager.HorizontalCreativeFlight;
        }

        if (_moveSensitivitySlider.IsSliding)
        {
            SettingsManager.MoveSensitivity = _moveSensitivitySlider.Value;
        }

        if (_lookSensitivitySlider.IsSliding)
        {
            SettingsManager.LookSensitivity = _lookSensitivitySlider.Value;
        }

        if (_gamepadCursorSpeedSlider.IsSliding)
        {
            SettingsManager.GamepadCursorSpeed = _gamepadCursorSpeedSlider.Value;
        }

        if (_gamepadDeadZoneSlider.IsSliding)
        {
            SettingsManager.GamepadDeadZone = _gamepadDeadZoneSlider.Value;
        }

        if (_creativeDigTimeSlider.IsSliding)
        {
            SettingsManager.CreativeDigTime = _creativeDigTimeSlider.Value;
        }

        if (_creativeReachSlider.IsSliding)
        {
            SettingsManager.CreativeReach = _creativeReachSlider.Value;
        }

        if (_holdDurationSlider.IsSliding)
        {
            SettingsManager.MinimumHoldDuration = _holdDurationSlider.Value;
        }

        if (_dragDistanceSlider.IsSliding)
        {
            SettingsManager.MinimumDragDistance = _dragDistanceSlider.Value;
        }

        _moveControlModeButton.Text =
            LanguageControl.Get("MoveControlMode", SettingsManager.MoveControlMode.ToString());
        _lookControlModeButton.Text =
            LanguageControl.Get("LookControlMode", SettingsManager.LookControlMode.ToString());
        _leftHandedLayoutButton.Text = SettingsManager.LeftHandedLayout
            ? LanguageControl.Get("Usual", "on")
            : LanguageControl.Get("Usual", "off");
        _flipVerticalAxisButton.Text = SettingsManager.FlipVerticalAxis
            ? LanguageControl.Get("Usual", "on")
            : LanguageControl.Get("Usual", "off");
        _autoJumpButton.Text = SettingsManager.AutoJump
            ? LanguageControl.Get("Usual", "on")
            : LanguageControl.Get("Usual", "off");
        _horizontalCreativeFlightButton.Text = SettingsManager.HorizontalCreativeFlight
            ? LanguageControl.Get("Usual", "on")
            : LanguageControl.Get("Usual", "off");
        _moveSensitivitySlider.Value = SettingsManager.MoveSensitivity;
        _moveSensitivitySlider.Text = MathUtils.Round(SettingsManager.MoveSensitivity * 10f).ToString(CultureInfo.InvariantCulture);
        _lookSensitivitySlider.Value = SettingsManager.LookSensitivity;
        _lookSensitivitySlider.Text = MathUtils.Round(SettingsManager.LookSensitivity * 10f).ToString(CultureInfo.InvariantCulture);
        _gamepadCursorSpeedSlider.Value = SettingsManager.GamepadCursorSpeed;
        _gamepadCursorSpeedSlider.Text = $"{SettingsManager.GamepadCursorSpeed:0.0}x";
        _gamepadDeadZoneSlider.Value = SettingsManager.GamepadDeadZone;
        _gamepadDeadZoneSlider.Text = $"{SettingsManager.GamepadDeadZone * 100f:0}%";
        _creativeDigTimeSlider.Value = SettingsManager.CreativeDigTime;
        _creativeDigTimeSlider.Text = $"{MathUtils.Round(1000f * SettingsManager.CreativeDigTime)}ms";
        _creativeReachSlider.Value = SettingsManager.CreativeReach;
        _creativeReachSlider.Text =
            string.Format(LanguageControl.Get(_typeName, 1), $"{SettingsManager.CreativeReach:0.0} ");
        _holdDurationSlider.Value = SettingsManager.MinimumHoldDuration;
        _holdDurationSlider.Text = $"{MathUtils.Round(1000f * SettingsManager.MinimumHoldDuration)}ms";
        _dragDistanceSlider.Value = SettingsManager.MinimumDragDistance;
        _dragDistanceSlider.Text =
            $"{MathUtils.Round(SettingsManager.MinimumDragDistance)} " + LanguageControl.Get(_typeName, 2);
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}

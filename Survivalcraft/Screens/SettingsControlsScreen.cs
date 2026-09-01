using System.Globalization;
using System.Xml.Linq;

namespace Game.Screens;

public class SettingsControlsScreen : Screen
{
    private const string _typeName = nameof(SettingsControlsScreen);

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
            SettingsManager.Current.MoveControlMode =
                (MoveControlMode)((int)(SettingsManager.Current.MoveControlMode + 1) %
                                  EnumUtils.GetEnumValues(typeof(MoveControlMode)).Count);
        }

        if (_lookControlModeButton.IsClicked)
        {
            SettingsManager.Current.LookControlMode =
                (LookControlMode)((int)(SettingsManager.Current.LookControlMode + 1) %
                                  EnumUtils.GetEnumValues(typeof(LookControlMode)).Count);
        }

        if (_leftHandedLayoutButton.IsClicked)
        {
            SettingsManager.Current.LeftHandedLayout = !SettingsManager.Current.LeftHandedLayout;
        }

        if (_flipVerticalAxisButton.IsClicked)
        {
            SettingsManager.Current.FlipVerticalAxis = !SettingsManager.Current.FlipVerticalAxis;
        }

        if (_autoJumpButton.IsClicked)
        {
            SettingsManager.Current.AutoJump = !SettingsManager.Current.AutoJump;
        }

        if (_horizontalCreativeFlightButton.IsClicked)
        {
            SettingsManager.Current.HorizontalCreativeFlight = !SettingsManager.Current.HorizontalCreativeFlight;
        }

        if (_moveSensitivitySlider.IsSliding)
        {
            SettingsManager.Current.MoveSensitivity = _moveSensitivitySlider.Value;
        }

        if (_lookSensitivitySlider.IsSliding)
        {
            SettingsManager.Current.LookSensitivity = _lookSensitivitySlider.Value;
        }

        if (_gamepadCursorSpeedSlider.IsSliding)
        {
            SettingsManager.Current.GamepadCursorSpeed = _gamepadCursorSpeedSlider.Value;
        }

        if (_gamepadDeadZoneSlider.IsSliding)
        {
            SettingsManager.Current.GamepadDeadZone = _gamepadDeadZoneSlider.Value;
        }

        if (_creativeDigTimeSlider.IsSliding)
        {
            SettingsManager.Current.CreativeDigTime = _creativeDigTimeSlider.Value;
        }

        if (_creativeReachSlider.IsSliding)
        {
            SettingsManager.Current.CreativeReach = _creativeReachSlider.Value;
        }

        if (_holdDurationSlider.IsSliding)
        {
            SettingsManager.Current.MinimumHoldDuration = _holdDurationSlider.Value;
        }

        if (_dragDistanceSlider.IsSliding)
        {
            SettingsManager.Current.MinimumDragDistance = _dragDistanceSlider.Value;
        }

        _moveControlModeButton.Text =
            LanguageManager.Get("MoveControlMode", SettingsManager.Current.MoveControlMode.ToString());
        _lookControlModeButton.Text =
            LanguageManager.Get("LookControlMode", SettingsManager.Current.LookControlMode.ToString());
        _leftHandedLayoutButton.Text = SettingsManager.Current.LeftHandedLayout
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _flipVerticalAxisButton.Text = SettingsManager.Current.FlipVerticalAxis
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _autoJumpButton.Text = SettingsManager.Current.AutoJump
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _horizontalCreativeFlightButton.Text = SettingsManager.Current.HorizontalCreativeFlight
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _moveSensitivitySlider.Value = SettingsManager.Current.MoveSensitivity;
        _moveSensitivitySlider.Text = MathUtils.Round(SettingsManager.Current.MoveSensitivity * 10f)
            .ToString(CultureInfo.InvariantCulture);
        _lookSensitivitySlider.Value = SettingsManager.Current.LookSensitivity;
        _lookSensitivitySlider.Text = MathUtils.Round(SettingsManager.Current.LookSensitivity * 10f)
            .ToString(CultureInfo.InvariantCulture);
        _gamepadCursorSpeedSlider.Value = SettingsManager.Current.GamepadCursorSpeed;
        _gamepadCursorSpeedSlider.Text = $"{SettingsManager.Current.GamepadCursorSpeed:0.0}x";
        _gamepadDeadZoneSlider.Value = SettingsManager.Current.GamepadDeadZone;
        _gamepadDeadZoneSlider.Text = $"{SettingsManager.Current.GamepadDeadZone * 100f:0}%";
        _creativeDigTimeSlider.Value = SettingsManager.Current.CreativeDigTime;
        _creativeDigTimeSlider.Text = $"{MathUtils.Round(1000f * SettingsManager.Current.CreativeDigTime)}ms";
        _creativeReachSlider.Value = SettingsManager.Current.CreativeReach;
        _creativeReachSlider.Text =
            string.Format(LanguageManager.Get(_typeName, 1), $"{SettingsManager.Current.CreativeReach:0.0} ");
        _holdDurationSlider.Value = SettingsManager.Current.MinimumHoldDuration;
        _holdDurationSlider.Text = $"{MathUtils.Round(1000f * SettingsManager.Current.MinimumHoldDuration)}ms";
        _dragDistanceSlider.Value = SettingsManager.Current.MinimumDragDistance;
        _dragDistanceSlider.Text =
            $"{MathUtils.Round(SettingsManager.Current.MinimumDragDistance)} " + LanguageManager.Get(_typeName, 2);
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}

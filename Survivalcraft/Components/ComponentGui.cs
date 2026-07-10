using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentGui : Component, IUpdateable, IDrawable
{
    public const string TypeName = "ComponentGui";

    public int CloseTime;

    private readonly LabelWidget _closeTimeLabel = new()
    {
        Name = "CloseTime", HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Near,
        IsVisible = false
    };

    private ButtonWidget _backButtonWidget = null!;

    private ButtonWidget _cameraButtonWidget = null!;

    private ButtonWidget _clothingButtonWidget = null!;

    public ComponentPlayer ComponentPlayer = null!;

    private ButtonWidget _creativeFlyButtonWidget = null!;

    private ButtonWidget _crouchButtonWidget = null!;

    private ButtonWidget _editItemButton = null!;

    private ButtonWidget _fogButtonWidget = null!;

    private GamepadHelpDialog? _gamepadHelpDialog;

    private bool _gamepadHelpMessageShown;

    private ButtonWidget _helpButtonWidget = null!;

    private ButtonWidget _inventoryButtonWidget = null!;

    private KeyboardHelpDialog? _keyboardHelpDialog;

    private bool _keyboardHelpMessageShown;

    private ContainerWidget _largeMessageWidget = null!;

    private double _lastMountableCreatureSearchTime;

    private PlayerContextAction? _lastResolvedContextAction;

    private ContainerWidget _leftControlsContainerWidget = null!;

    private ButtonWidget _lightningButtonWidget = null!;

    private ContainerWidget _lookContainerWidget = null!;

    private ContainerWidget _lookPadContainerWidget = null!;

    private ContainerWidget _lookRectangleContainerWidget = null!;

    private RectangleWidget _lookRectangleWidget = null!;

    private Message? _message;

    private MessageWidget _messageWidget = null!;

    private ModalPanelAnimationData? _modalPanelAnimationData;

    private ContainerWidget _modalPanelContainerWidget = null!;

    private ButtonWidget _moreButtonWidget = null!;

    private Widget _moreContentsWidget = null!;

    private ButtonWidget _mountButtonWidget = null!;

    private string _mountButtonDefaultText = string.Empty;

    private ContainerWidget _moveButtonsContainerWidget = null!;

    private ContainerWidget _moveContainerWidget = null!;

    private ContainerWidget _movePadContainerWidget = null!;

    private ContainerWidget _moveRectangleContainerWidget = null!;

    private RectangleWidget _moveRectangleWidget = null!;

    private ButtonWidget _photoButtonWidget = null!;

    private ButtonWidget _precipitationButtonWidget = null!;

    private ContainerWidget _rightControlsContainerWidget = null!;

    private float _sidePanelsFactor;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    public SubsystemGameInfo SubsystemGameInfo = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTimeOfDay _subsystemTimeOfDay = null!;

    private SubsystemWeather _subsystemWeather = null!;

    private ButtonWidget _timeOfDayButtonWidget = null!;

    public ContainerWidget ControlsContainerWidget { get; set; } = null!;

    public TouchInputWidget ViewWidget { get; set; } = null!;

    public TouchInputWidget MoveWidget { get; set; } = null!;

    public MoveRoseWidget MoveRoseWidget { get; set; } = null!;

    public TouchInputWidget LookWidget { get; set; } = null!;

    public ShortInventoryWidget ShortInventoryWidget { get; set; } = null!;

    public ValueBarWidget HealthBarWidget { get; set; } = null!;

    public ValueBarWidget FoodBarWidget { get; set; } = null!;

    public ValueBarWidget TemperatureBarWidget { get; set; } = null!;

    public LabelWidget LevelLabelWidget { get; set; } = null!;

    public Widget? ModalPanelWidget
    {
        get => _modalPanelContainerWidget.Children.Count <= 0 ? null : _modalPanelContainerWidget.Children[0];
        set
        {
            if (value != ModalPanelWidget)
            {
                if (_modalPanelAnimationData != null)
                {
                    EndModalPanelAnimation();
                }

                _modalPanelAnimationData = new ModalPanelAnimationData
                {
                    OldWidget = ModalPanelWidget,
                    NewWidget = value
                };
                if (value != null)
                {
                    value.HorizontalAlignment = WidgetAlignment.Center;
                    _modalPanelContainerWidget.Children.Insert(0, value);
                }

                UpdateModalPanelAnimation();
                ComponentPlayer.GameWidget.Input.Clear();
                ComponentPlayer.ComponentInput.SetSplitSourceInventoryAndSlot(null, -1);
            }
        }
    }

    public int[] DrawOrders => [9];

    public void Draw(Camera camera, int drawOrder)
    {
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (CloseTime > 0)
        {
            _closeTimeLabel.IsVisible = true;
            _closeTimeLabel.Text = "距离服务器关闭还有" + CloseTime + "秒";
        }

        if (Time.PeriodicEvent(1.0, 0.9))
        {
            CloseTime--;
        }

        if (_closeTimeLabel.IsVisible && CloseTime == 0)
        {
            CommonLib.Net.StopImmediate();
        }

        HandleInput();
        UpdateWidgets();
    }

    public void DisplayLargeMessage(string largeText, string smallText, float duration, float delay)
    {
        _message = new Message
        {
            LargeText = largeText,
            SmallText = smallText,
            Duration = duration,
            StartTime = Time.RealTime + delay
        };
    }

    public void DisplaySmallMessage(string text, Color color, bool blinking, bool playNotificationSound)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        _messageWidget.DisplayMessage(text, color, blinking);
        if (CommonLib.WorkType != WorkType.Local)
        {
            playNotificationSound = ComponentPlayer.PlayerData.IsMainPlayer;
        }

        if (playNotificationSound)
        {
            _subsystemAudio.PlaySound("Audio/UI/Message", 1f, 0f, ComponentPlayer.ComponentBody.Position, 2f, false);
        }
    }

    public bool IsGameMenuDialogVisible()
    {
        return false;
    }


    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        SubsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        ComponentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemWeather = Project.FindSubsystem<SubsystemWeather>(true)!;
        var guiWidget = ComponentPlayer.GuiWidget;
        guiWidget.Children.Insert(0, _closeTimeLabel);
        _backButtonWidget = guiWidget.Children.Find<ButtonWidget>("BackButton")!;
        _inventoryButtonWidget = guiWidget.Children.Find<ButtonWidget>("InventoryButton")!;
        _clothingButtonWidget = guiWidget.Children.Find<ButtonWidget>("ClothingButton")!;
        _moreButtonWidget = guiWidget.Children.Find<ButtonWidget>("MoreButton")!;
        _moreContentsWidget = guiWidget.Children.Find<Widget>("MoreContents")!;
        _helpButtonWidget = guiWidget.Children.Find<ButtonWidget>("HelpButton")!;
        _photoButtonWidget = guiWidget.Children.Find<ButtonWidget>("PhotoButton")!;
        _lightningButtonWidget = guiWidget.Children.Find<ButtonWidget>("LightningButton")!;
        _precipitationButtonWidget = guiWidget.Children.Find<ButtonWidget>("PrecipitationButton")!;
        _fogButtonWidget = guiWidget.Children.Find<ButtonWidget>("FogButton")!;
        _timeOfDayButtonWidget = guiWidget.Children.Find<ButtonWidget>("TimeOfDayButton")!;
        _cameraButtonWidget = guiWidget.Children.Find<ButtonWidget>("CameraButton")!;
        _creativeFlyButtonWidget = guiWidget.Children.Find<ButtonWidget>("CreativeFlyButton")!;
        _crouchButtonWidget = guiWidget.Children.Find<ButtonWidget>("CrouchButton")!;
        _mountButtonWidget = guiWidget.Children.Find<ButtonWidget>("MountButton")!;
        _mountButtonDefaultText = _mountButtonWidget.Text;
        _editItemButton = guiWidget.Children.Find<ButtonWidget>("EditItemButton")!;
        MoveWidget = guiWidget.Children.Find<TouchInputWidget>("Move")!;
        MoveRoseWidget = guiWidget.Children.Find<MoveRoseWidget>("MoveRose")!;
        LookWidget = guiWidget.Children.Find<TouchInputWidget>("Look")!;
        ViewWidget = ComponentPlayer.ViewWidget;
        HealthBarWidget = guiWidget.Children.Find<ValueBarWidget>("HealthBar")!;
        FoodBarWidget = guiWidget.Children.Find<ValueBarWidget>("FoodBar")!;
        TemperatureBarWidget = guiWidget.Children.Find<ValueBarWidget>("TemperatureBar")!;
        LevelLabelWidget = guiWidget.Children.Find<LabelWidget>("LevelLabel")!;
        _modalPanelContainerWidget = guiWidget.Children.Find<ContainerWidget>("ModalPanelContainer")!;
        ControlsContainerWidget = guiWidget.Children.Find<ContainerWidget>("ControlsContainer")!;
        _leftControlsContainerWidget = guiWidget.Children.Find<ContainerWidget>("LeftControlsContainer")!;
        _rightControlsContainerWidget = guiWidget.Children.Find<ContainerWidget>("RightControlsContainer")!;
        _moveContainerWidget = guiWidget.Children.Find<ContainerWidget>("MoveContainer")!;
        _lookContainerWidget = guiWidget.Children.Find<ContainerWidget>("LookContainer")!;
        _moveRectangleWidget = guiWidget.Children.Find<RectangleWidget>("MoveRectangle")!;
        _lookRectangleWidget = guiWidget.Children.Find<RectangleWidget>("LookRectangle")!;
        _moveRectangleContainerWidget = guiWidget.Children.Find<ContainerWidget>("MoveRectangleContainer")!;
        _lookRectangleContainerWidget = guiWidget.Children.Find<ContainerWidget>("LookRectangleContainer")!;
        _moveRectangleWidget = guiWidget.Children.Find<RectangleWidget>("MoveRectangle")!;
        _lookRectangleWidget = guiWidget.Children.Find<RectangleWidget>("LookRectangle")!;
        _movePadContainerWidget = guiWidget.Children.Find<ContainerWidget>("MovePadContainer")!;
        _lookPadContainerWidget = guiWidget.Children.Find<ContainerWidget>("LookPadContainer")!;
        _moveButtonsContainerWidget = guiWidget.Children.Find<ContainerWidget>("MoveButtonsContainer")!;
        ShortInventoryWidget = guiWidget.Children.Find<ShortInventoryWidget>("ShortInventory")!;
        _largeMessageWidget = guiWidget.Children.Find<ContainerWidget>("LargeMessage")!;
        _messageWidget = guiWidget.Children.Find<MessageWidget>("Message")!;
        _keyboardHelpMessageShown = valuesDictionary.GetValue<bool>("KeyboardHelpMessageShown");
        _keyboardHelpMessageShown = valuesDictionary.GetValue<bool>("KeyboardHelpMessageShown");
        _gamepadHelpMessageShown = valuesDictionary.GetValue<bool>("GamepadHelpMessageShown");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("KeyboardHelpMessageShown", _keyboardHelpMessageShown);
        valuesDictionary.SetValue("GamepadHelpMessageShown", _gamepadHelpMessageShown);
    }

    public override void OnEntityAdded()
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        ShortInventoryWidget.AssignComponents(ComponentPlayer.ComponentMiner.Inventory);
    }

    public override void OnEntityRemoved()
    {
        if (RunMode.Value is RunModeType.Gui)
        {
            ShortInventoryWidget.AssignComponents(null);
        }

        _message = null;
    }

    public override void Dispose()
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        ComponentPlayer.GuiWidget.Children.Remove(_closeTimeLabel);
        ModalPanelWidget = null;
        _keyboardHelpDialog = null;
        ShortInventoryWidget.AssignComponents(null);
    }

    public void UpdateSidePanelsAnimation()
    {
        var num = MathUtils.Min(Time.FrameDuration, 0.1f);
        var flag = ModalPanelWidget != null &&
                   _modalPanelAnimationData is not { NewWidget: null };
        float num2 = !(ComponentPlayer.ComponentInput.IsControlledByTouch | flag) ? 1 : 0;
        var x = num2 - _sidePanelsFactor;
        if (MathUtils.Abs(x) > 0.01f)
        {
            _sidePanelsFactor += MathUtils.Clamp(12f * MathUtils.PowSign(x, 0.75f) * num, 0f - MathUtils.Abs(x),
                MathUtils.Abs(x));
        }
        else
        {
            _sidePanelsFactor = num2;
        }

        _leftControlsContainerWidget.RenderTransform =
            Matrix.CreateTranslation(_leftControlsContainerWidget.ActualSize.X * (0f - _sidePanelsFactor), 0f, 0f);
        _rightControlsContainerWidget.RenderTransform =
            Matrix.CreateTranslation(_rightControlsContainerWidget.ActualSize.X * _sidePanelsFactor, 0f, 0f);
    }

    public void UpdateModalPanelAnimation()
    {
        _modalPanelAnimationData?.Factor += 6f * MathUtils.Min(Time.FrameDuration, 0.1f);
        if (_modalPanelAnimationData?.Factor < 1f)
        {
            var factor = _modalPanelAnimationData.Factor;
            var num = 0.5f + 0.5f * MathUtils.Pow(1f - factor, 0.1f);
            var num2 = 0.5f + 0.5f * MathUtils.Pow(factor, 0.1f);
            var s = 1f - factor;
            if (_modalPanelAnimationData.OldWidget != null)
            {
                var actualSize = _modalPanelAnimationData.OldWidget.ActualSize;
                _modalPanelAnimationData.OldWidget.ColorTransform = Color.White * s;
                _modalPanelAnimationData.OldWidget.RenderTransform =
                    Matrix.CreateTranslation((0f - actualSize.X) / 2f, (0f - actualSize.Y) / 2f, 0f) *
                    Matrix.CreateScale(num, num, 1f) *
                    Matrix.CreateTranslation(actualSize.X / 2f, actualSize.Y / 2f, 0f);
            }

            if (_modalPanelAnimationData.NewWidget == null)
            {
                return;
            }

            var actualSize2 = _modalPanelAnimationData.NewWidget.ActualSize;
            _modalPanelAnimationData.NewWidget.ColorTransform = Color.White * factor;
            _modalPanelAnimationData.NewWidget.RenderTransform =
                Matrix.CreateTranslation((0f - actualSize2.X) / 2f, (0f - actualSize2.Y) / 2f, 0f) *
                Matrix.CreateScale(num2, num2, 1f) *
                Matrix.CreateTranslation(actualSize2.X / 2f, actualSize2.Y / 2f, 0f);
        }
        else
        {
            EndModalPanelAnimation();
        }
    }

    public void EndModalPanelAnimation()
    {
        if (_modalPanelAnimationData?.OldWidget != null)
        {
            _modalPanelContainerWidget.Children.Remove(_modalPanelAnimationData.OldWidget);
        }

        if (_modalPanelAnimationData?.NewWidget != null)
        {
            _modalPanelAnimationData.NewWidget.ColorTransform = Color.White;
            _modalPanelAnimationData.NewWidget.RenderTransform = Matrix.Identity;
        }

        _modalPanelAnimationData = null;
    }

    public void UpdateWidgets()
    {
        var componentRider = ComponentPlayer.ComponentRider;
        var componentSleep = ComponentPlayer.ComponentSleep;
        var componentInput = ComponentPlayer.ComponentInput;
        var worldSettings = SubsystemGameInfo.WorldSettings;
        var gameMode = worldSettings.GameMode;
        UpdateSidePanelsAnimation();
        if (_modalPanelAnimationData != null)
        {
            UpdateModalPanelAnimation();
        }

        if (_message != null)
        {
            var realTime = Time.RealTime;
            _largeMessageWidget.IsVisible = true;
            var labelWidget = _largeMessageWidget.Children.Find<LabelWidget>("LargeLabel")!;
            var labelWidget2 = _largeMessageWidget.Children.Find<LabelWidget>("SmallLabel")!;
            labelWidget.Text = _message.LargeText;
            labelWidget2.Text = _message.SmallText;
            labelWidget.IsVisible = !string.IsNullOrEmpty(_message.LargeText);
            labelWidget2.IsVisible = !string.IsNullOrEmpty(_message.SmallText);
            var num = (float)MathUtils.Min(MathUtils.Saturate(2.0 * (realTime - _message.StartTime)),
                MathUtils.Saturate(2.0 * (_message.StartTime + _message.Duration - realTime)));
            labelWidget.Color = new Color(num, num, num, num);
            labelWidget2.Color = new Color(num, num, num, num);
            if (Time.RealTime > _message.StartTime + _message.Duration)
            {
                _message = null;
            }
        }
        else
        {
            _largeMessageWidget.IsVisible = false;
        }

        ControlsContainerWidget.IsVisible = ComponentPlayer.PlayerData.IsReadyForPlaying &&
                                            ComponentPlayer.GameWidget.ActiveCamera.IsEntityControlEnabled &&
                                            componentSleep.SleepFactor <= 0f;
        _moveRectangleContainerWidget.IsVisible =
            !SettingsManager.Current.HideMoveLookPads && componentInput.IsControlledByTouch;
        _lookRectangleContainerWidget.IsVisible = !SettingsManager.Current.HideMoveLookPads &&
                                                  componentInput.IsControlledByTouch &&
                                                  (SettingsManager.Current.LookControlMode != LookControlMode.EntireScreen ||
                                                   SettingsManager.Current.MoveControlMode != MoveControlMode.Buttons);
        _lookPadContainerWidget.IsVisible = SettingsManager.Current.LookControlMode != LookControlMode.SplitTouch;
        MoveRoseWidget.IsVisible = componentInput.IsControlledByTouch;
        _moreContentsWidget.IsVisible = _moreButtonWidget.IsChecked;
        HealthBarWidget.IsVisible = gameMode != GameMode.Creative;
        FoodBarWidget.IsVisible = gameMode != 0 && worldSettings.AreAdventureSurvivalMechanicsEnabled;
        TemperatureBarWidget.IsVisible = gameMode != 0 && worldSettings.AreAdventureSurvivalMechanicsEnabled;
        LevelLabelWidget.IsVisible = gameMode != 0 && worldSettings.AreAdventureSurvivalMechanicsEnabled;
        _creativeFlyButtonWidget.IsVisible = gameMode == GameMode.Creative;
        _timeOfDayButtonWidget.IsVisible = gameMode == GameMode.Creative;
        _lightningButtonWidget.IsVisible = gameMode == GameMode.Creative;
        _precipitationButtonWidget.IsVisible = gameMode == GameMode.Creative && worldSettings.AreWeatherEffectsEnabled;
        _fogButtonWidget.IsVisible = gameMode == GameMode.Creative && worldSettings.AreWeatherEffectsEnabled;
        _moveButtonsContainerWidget.IsVisible = SettingsManager.Current.MoveControlMode == MoveControlMode.Buttons;
        _movePadContainerWidget.IsVisible = SettingsManager.Current.MoveControlMode == MoveControlMode.Pad;
        if (SettingsManager.Current.LeftHandedLayout)
        {
            _moveContainerWidget.HorizontalAlignment = WidgetAlignment.Far;
            _lookContainerWidget.HorizontalAlignment = WidgetAlignment.Near;
            _moveRectangleWidget.FlipHorizontal = true;
            _lookRectangleWidget.FlipHorizontal = false;
        }
        else
        {
            _moveContainerWidget.HorizontalAlignment = WidgetAlignment.Near;
            _lookContainerWidget.HorizontalAlignment = WidgetAlignment.Far;
            _moveRectangleWidget.FlipHorizontal = false;
            _lookRectangleWidget.FlipHorizontal = true;
        }

        _precipitationButtonWidget.IsChecked = _subsystemWeather.IsPrecipitationStarted;
        _fogButtonWidget.IsChecked = _subsystemWeather.IsFogStarted;
        _crouchButtonWidget.IsChecked = ComponentPlayer.ComponentBody.TargetCrouchFactor > 0f;
        _creativeFlyButtonWidget.IsChecked = ComponentPlayer.ComponentLocomotion.IsCreativeFlyEnabled;
        _inventoryButtonWidget.IsChecked = IsInventoryVisible();
        _clothingButtonWidget.IsChecked = IsClothingVisible();
        if (IsActiveSlotEditable() || ComponentPlayer.ComponentBlockHighlight.NearbyEditableCell.HasValue)
        {
            _crouchButtonWidget.IsVisible = false;
            _mountButtonWidget.IsVisible = false;
            _editItemButton.IsVisible = true;
            _mountButtonWidget.Text = _mountButtonDefaultText;
            _lastResolvedContextAction = null;
        }
        else if (componentRider is { Mount: not null })
        {
            _crouchButtonWidget.IsVisible = false;
            _mountButtonWidget.IsChecked = true;
            _mountButtonWidget.IsVisible = true;
            _editItemButton.IsVisible = false;
            _mountButtonWidget.Text = _mountButtonDefaultText;
            _lastResolvedContextAction = null;
        }
        else
        {
            _mountButtonWidget.IsChecked = false;
            if (Time.FrameStartTime - _lastMountableCreatureSearchTime > 0.5)
            {
                _lastMountableCreatureSearchTime = Time.FrameStartTime;
                _lastResolvedContextAction = CurrentModRuntime.Value?.ContextActions.Resolve(
                    new PlayerContextActionQueryContext(ComponentPlayer, this));
                if (_lastResolvedContextAction != null)
                {
                    _crouchButtonWidget.IsVisible = false;
                    _mountButtonWidget.IsVisible = true;
                    _mountButtonWidget.IsChecked = _lastResolvedContextAction.IsChecked;
                    _mountButtonWidget.Text = _lastResolvedContextAction.Label;
                    _editItemButton.IsVisible = false;
                }
                else if (componentRider.FindNearestMount() != null)
                {
                    _crouchButtonWidget.IsVisible = false;
                    _mountButtonWidget.IsVisible = true;
                    _mountButtonWidget.Text = _mountButtonDefaultText;
                    _editItemButton.IsVisible = false;
                }
                else
                {
                    _crouchButtonWidget.IsVisible = true;
                    _mountButtonWidget.IsVisible = false;
                    _mountButtonWidget.Text = _mountButtonDefaultText;
                    _editItemButton.IsVisible = false;
                }
            }
        }

        if (!ComponentPlayer.IsAddedToProject || ComponentPlayer.ComponentHealth.Health == 0f ||
            componentSleep.IsSleeping || ComponentPlayer.ComponentSickness.IsPuking)
        {
            ModalPanelWidget = null;
        }

        if (ComponentPlayer.ComponentSickness.IsSick)
        {
            ComponentPlayer.ComponentGui.HealthBarWidget.LitBarColor = new Color(166, 175, 103);
        }
        else
        {
            ComponentPlayer.ComponentGui.HealthBarWidget.LitBarColor = ComponentPlayer.ComponentFlu.HasFlu
                ? new Color(0, 48, 255)
                : new Color(224, 24, 0);
        }
    }

    public void HandleInput()
    {
        var input = ComponentPlayer.GameWidget.Input;
        var playerInput = ComponentPlayer.ComponentInput.PlayerInput;
        var componentRider = ComponentPlayer.ComponentRider;

        if (ComponentPlayer.GameWidget.ActiveCamera.IsEntityControlEnabled)
        {
            if (!_keyboardHelpMessageShown &&
                (ComponentPlayer.PlayerData.InputDevice & WidgetInputDevice.Keyboard) != 0 &&
                Time.PeriodicEvent(7.0, 0.0))
            {
                _keyboardHelpMessageShown = true;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 1), Color.White, true, true);
            }
            else if (!_gamepadHelpMessageShown &&
                     (ComponentPlayer.PlayerData.InputDevice & WidgetInputDevice.Gamepads) != 0 &&
                     Time.PeriodicEvent(7.0, 0.0))
            {
                _gamepadHelpMessageShown = true;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 2), Color.White, true, true);
            }
        }

        if (playerInput.KeyboardHelp)
        {
            _keyboardHelpDialog ??= new KeyboardHelpDialog();
            if (_keyboardHelpDialog.ParentWidget != null)
            {
                DialogsManager.HideDialog(_keyboardHelpDialog);
            }
            else
            {
                DialogsManager.ShowDialog(ComponentPlayer.GuiWidget, _keyboardHelpDialog);
            }
        }

        // 创建队伍
        if (playerInput.CreateTeam)
        {
            var netPanelWidget = ComponentPlayer.GuiWidget.Children.Find<NetPanelWidget>(null, false);
            netPanelWidget?.CreateTeam();
        }

        // 加入队伍
        if (playerInput.JoinTeam)
        {
            var netPanelWidget = ComponentPlayer.GuiWidget.Children.Find<NetPanelWidget>(null, false);
            netPanelWidget?.JoinTeam();
        }

        // 离开队伍
        if (playerInput.LeaveTeam)
        {
            var netPanelWidget = ComponentPlayer.GuiWidget.Children.Find<NetPanelWidget>(null, false);
            netPanelWidget?.LeaveTeam();
        }

        // 切换玩家展示类型
        if (playerInput.TogglePlayerShowType)
        {
            var netPanelWidget = ComponentPlayer.GuiWidget.Children.Find<NetPanelWidget>(null, false);
            netPanelWidget?.CycleSwitch();
        }

        if (playerInput.GamepadHelp)
        {
            if (_gamepadHelpDialog == null)
            {
                _gamepadHelpDialog = new GamepadHelpDialog();
            }

            if (_gamepadHelpDialog.ParentWidget != null)
            {
                DialogsManager.HideDialog(_gamepadHelpDialog);
            }
            else
            {
                DialogsManager.ShowDialog(ComponentPlayer.GuiWidget, _gamepadHelpDialog);
            }
        }

        if (_helpButtonWidget.IsClicked)
        {
            ScreensManager.SwitchScreen("Help");
        }

        if (playerInput.ToggleInventory || _inventoryButtonWidget.IsClicked)
        {
            if (IsInventoryVisible())
            {
                ModalPanelWidget = null;
            }
            else
            {
                ModalPanelWidget = ComponentPlayer.ComponentMiner.Inventory is ComponentCreativeInventory
                    ? new CreativeInventoryWidget(ComponentPlayer.Entity)
                    : new FullInventoryWidget(ComponentPlayer.ComponentMiner.Inventory,
                        ComponentPlayer.Entity.FindComponent<ComponentCraftingTable>(true)!
                    );
            }
        }

        if (playerInput.ToggleClothing || _clothingButtonWidget.IsClicked)
        {
            var clothing = new ClothingWidget(ComponentPlayer);
            ModalPanelWidget = IsClothingVisible() ? null : clothing;
        }

        if (_crouchButtonWidget.IsClicked || playerInput.ToggleSneak)
        {
            if (ComponentPlayer.ComponentBody.TargetCrouchFactor == 0f)
            {
                if (ComponentPlayer.ComponentBody.StandingOnValue.HasValue)
                {
                    ComponentPlayer.ComponentBody.TargetCrouchFactor = 1f;
                    DisplaySmallMessage(LanguageManager.Get(TypeName, 4), Color.White, false, false);
                }
            }
            else
            {
                ComponentPlayer.ComponentBody.TargetCrouchFactor = 0f;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 3), Color.White, false, false);
            }
        }

        if ((_mountButtonWidget.IsClicked || playerInput.ToggleMount))
        {
            var flag = componentRider.Mount != null;
            if (!flag && _lastResolvedContextAction != null)
            {
                try
                {
                    _lastResolvedContextAction.Execute(
                        new PlayerContextActionExecutionContext(ComponentPlayer, this));
                }
                catch (Exception exception)
                {
                    Log.Error($"Mod context action failed: {exception}");
                }
            }
            else if (flag)
            {
                componentRider.StartDismounting();
            }
            else
            {
                var componentMount = componentRider.FindNearestMount();
                if (componentMount != null)
                {
                    componentRider.StartMounting(componentMount);
                }
            }

            if (componentRider.Mount != null != flag)
            {
                DisplaySmallMessage(
                    componentRider.Mount != null
                        ? LanguageManager.Get(TypeName, 5)
                        : LanguageManager.Get(TypeName, 6),
                    Color.White,
                    false,
                    false
                );
            }
        }

        if ((_editItemButton.IsClicked || playerInput.EditItem) &&
            ComponentPlayer.ComponentBlockHighlight.NearbyEditableCell.HasValue)
        {
            var value = ComponentPlayer.ComponentBlockHighlight.NearbyEditableCell.Value;
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(value.X, value.Y, value.Z);
            var contents = Terrain.ExtractContents(cellValue);
            var editBlockContext = new BlockEditContext(value.X, value.Y, value.Z, cellValue, ComponentPlayer);
            CurrentModRuntime.Value?.BlockBehaviors.Invoke(editBlockContext);
            if (editBlockContext is { Cancel: false, Handled: false })
            {
                var blockBehaviors =
                    _subsystemBlockBehaviors.GetBlockBehaviors(contents, ComponentPlayer.ComponentMiner, value);
                for (var i = 0;
                     i < blockBehaviors.Length && !blockBehaviors[i]
                         .OnEditBlock(value.X, value.Y, value.Z, cellValue, ComponentPlayer);
                     i++)
                {
                }
            }
        }
        else if ((_editItemButton.IsClicked || playerInput.EditItem) && IsActiveSlotEditable())
        {
            var inventory = ComponentPlayer.ComponentMiner.Inventory;
            var activeSlotIndex = inventory.ActiveSlotIndex;
            var num = Terrain.ExtractContents(inventory.GetSlotValue(activeSlotIndex));
            if (BlocksManager.Blocks[num].Editable)
            {
                var editInventoryContext =
                    new BlockEditInventoryItemContext(inventory, activeSlotIndex, ComponentPlayer);
                CurrentModRuntime.Value?.BlockBehaviors.Invoke(editInventoryContext);
                if (!editInventoryContext.Cancel && !editInventoryContext.Handled)
                {
                    var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num);
                    for (var i = 0;
                         i < blockBehaviors.Length && !blockBehaviors[i]
                             .OnEditInventoryItem(inventory, activeSlotIndex, ComponentPlayer);
                         i++)
                    {
                    }
                }
            }
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_creativeFlyButtonWidget.IsClicked || playerInput.ToggleCreativeFly) && componentRider.Mount == null)
        {
            var isCreativeFlyEnabled = ComponentPlayer.ComponentLocomotion.IsCreativeFlyEnabled;
            ComponentPlayer.ComponentLocomotion.IsCreativeFlyEnabled = !isCreativeFlyEnabled;
            if (ComponentPlayer.ComponentLocomotion.IsCreativeFlyEnabled != isCreativeFlyEnabled)
            {
                if (ComponentPlayer.ComponentLocomotion.IsCreativeFlyEnabled)
                {
                    ComponentPlayer.ComponentLocomotion.JumpOrder = 1f;
                    DisplaySmallMessage(LanguageManager.Get(TypeName, 7), Color.White, false, false);
                }
                else
                {
                    DisplaySmallMessage(LanguageManager.Get(TypeName, 8), Color.White, false, false);
                }
            }

            CommonLib.Net.QueuePackage(new ComponentPlayerPackage(ComponentPlayer,
                ComponentPlayerPackage.PlayerAction.CreativeFlyChange));
        }

        if (!ComponentPlayer.ComponentInput.IsControlledByVr &&
            (_cameraButtonWidget.IsClicked || playerInput.SwitchCameraMode))
        {
            ChangeCameraMode();
        }

        if (_photoButtonWidget.IsClicked || playerInput.TakeScreenshot)
        {
            ScreenCaptureManager.CapturePhoto(
                delegate { DisplaySmallMessage(LanguageManager.Get(TypeName, 13), Color.White, false, false); },
                delegate { DisplaySmallMessage(LanguageManager.Get(TypeName, 14), Color.White, false, false); });
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_lightningButtonWidget.IsClicked || playerInput.Lighting))
        {
            var matrix = Matrix.CreateFromQuaternion(ComponentPlayer.ComponentCreatureModel.EyeRotation);
            if (CommonLib.WorkType != WorkType.Client)
            {
                Project.FindSubsystem<SubsystemWeather>(true)!
                    .ManualLightingStrike(ComponentPlayer.ComponentCreatureModel.EyePosition, matrix.Forward);
                _subsystemWeather.ManualLightingStrike(ComponentPlayer.ComponentCreatureModel.EyePosition,
                    matrix.Forward);
            }
            else
            {
                CommonLib.Net.QueuePackage(new SubsystemSkyPackage(ComponentPlayer.ComponentCreatureModel.EyePosition,
                    matrix.Forward));
            }
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_precipitationButtonWidget.IsClicked || playerInput.Precipitation))
        {
            if (_subsystemWeather.IsPrecipitationStarted)
            {
                _subsystemWeather.ManualPrecipitationEnd();
                DisplaySmallMessage(LanguageManager.Get(TypeName, 20), Color.White, false, false);
                CommonLib.Net.QueuePackage(new SubsystemWeatherPackage(1));
            }
            else
            {
                _subsystemWeather.ManualPrecipitationStart();
                DisplaySmallMessage(LanguageManager.Get(TypeName, 21), Color.White, false, false);
                CommonLib.Net.QueuePackage(new SubsystemWeatherPackage(2));
            }
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_fogButtonWidget.IsClicked || playerInput.Fog))
        {
            if (_subsystemWeather.IsFogStarted)
            {
                _subsystemWeather.ManualFogEnd();
                DisplaySmallMessage(LanguageManager.Get(TypeName, 22), Color.White, false, false);
                CommonLib.Net.QueuePackage(new SubsystemWeatherPackage(3));
            }
            else
            {
                _subsystemWeather.ManualFogStart();
                DisplaySmallMessage(LanguageManager.Get(TypeName, 23), Color.White, false, false);
                CommonLib.Net.QueuePackage(new SubsystemWeatherPackage(4));
            }
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_timeOfDayButtonWidget.IsClicked || playerInput.TimeOfDay))
        {
            var num2 = IntervalUtils.Interval(_subsystemTimeOfDay.TimeOfDay, _subsystemTimeOfDay.MidDawn);
            var num3 = IntervalUtils.Interval(_subsystemTimeOfDay.TimeOfDay, _subsystemTimeOfDay.Midday);
            var num4 = IntervalUtils.Interval(_subsystemTimeOfDay.TimeOfDay, _subsystemTimeOfDay.MidDusk);
            var num5 = IntervalUtils.Interval(_subsystemTimeOfDay.TimeOfDay, _subsystemTimeOfDay.Midnight);
            var num6 = MathUtils.Min(num2, num3, num4, num5);
            byte? type = null;
            if (num2.CloseTo(num6))
            {
                _subsystemTimeOfDay.TimeOfDayOffset += num2;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 15), Color.White, false, false);
                type = 0;
            }
            else if (num3.CloseTo(num6))
            {
                _subsystemTimeOfDay.TimeOfDayOffset += num3;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 16), Color.White, false, false);
                type = 1;
            }
            else if (num4.CloseTo(num6))
            {
                _subsystemTimeOfDay.TimeOfDayOffset += num4;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 17), Color.White, false, false);
                type = 2;
            }
            else if (num5.CloseTo(num6))
            {
                _subsystemTimeOfDay.TimeOfDayOffset += num5;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 18), Color.White, false, false);
                type = 3;
            }

            if (type.HasValue)
            {
                CommonLib.Net.QueuePackage(new SubsystemTimePackage(
                    _subsystemTimeOfDay.SubsystemGameInfo.TotalElapsedGameTime,
                    _subsystemTimeOfDay.TimeOfDayOffset));
            }
        }

        if (ModalPanelWidget != null)
        {
            if (input.Cancel || input.Back || _backButtonWidget.IsClicked)
            {
                ModalPanelWidget = null;
            }
        }
        else if (input.Back || _backButtonWidget.IsClicked)
        {
            DialogsManager.ShowDialog(ComponentPlayer.GuiWidget, new GameMenuDialog(ComponentPlayer));
        }
    }

    public bool IsClothingVisible()
    {
        return ModalPanelWidget is ClothingWidget;
    }

    public bool IsInventoryVisible()
    {
        return ModalPanelWidget is CreativeInventoryWidget or FullInventoryWidget;
    }

    public bool IsActiveSlotEditable()
    {
        var inventory = ComponentPlayer.ComponentMiner.Inventory;
        var activeSlotIndex = inventory.ActiveSlotIndex;
        var num = Terrain.ExtractContents(inventory.GetSlotValue(activeSlotIndex));
        return BlocksManager.Blocks[num].Editable;
    }

    public class ModalPanelAnimationData
    {
        public float Factor;

        public Widget? NewWidget;

        public Widget? OldWidget;
    }

    public class Message
    {
        public float Duration;

        public string LargeText = string.Empty;

        public string SmallText = string.Empty;

        public double StartTime;
    }

    private void ChangeCameraMode()
    {
        var gameWidget = ComponentPlayer.GameWidget;
        if (gameWidget.ActiveCamera is FppCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<TppCamera>()!;
            DisplaySmallMessage(LanguageManager.Get(TypeName, 9), Color.White, false, false);
        }
        else if (gameWidget.ActiveCamera is TppCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<OrbitCamera>()!;
            DisplaySmallMessage(LanguageManager.Get(TypeName, 10), Color.White, false, false);
        }
        else if (gameWidget.ActiveCamera is OrbitCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<FixedCamera>()!;
            DisplaySmallMessage(LanguageManager.Get(TypeName, 11), Color.White, false, false);
        }
        else
        {
            var isAdmin = ComponentPlayer.PlayerData is { ServerManager: true } or { ServerMaster: true };
            if ((SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative || isAdmin) &&
                gameWidget.ActiveCamera is FixedCamera)
            {
                gameWidget.ActiveCamera = gameWidget.FindCamera<DebugCamera>()!;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 19), Color.White, false, false);
            }
            else
            {
                gameWidget.ActiveCamera = gameWidget.FindCamera<FppCamera>()!;
                DisplaySmallMessage(LanguageManager.Get(TypeName, 12), Color.White, false, false);
            }
        }
    }
}

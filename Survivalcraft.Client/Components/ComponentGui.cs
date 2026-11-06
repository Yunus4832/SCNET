using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Network.Transport;
using Game.Platforms;

namespace Game.Components;

public class ComponentGui : Component, IUpdateable, IDrawable
{
    public UpdateExecutionSide UpdateSide => ComponentCharacter is { IsLocallyControlled: true }
        ? UpdateExecutionSide.Presentation
        : UpdateExecutionSide.Disabled;

    public const string TypeName = nameof(ComponentGui);

    private ButtonWidget _backButtonWidget = null!;

    private ButtonWidget _cameraButtonWidget = null!;

    private ButtonWidget _clothingButtonWidget = null!;

    public ComponentCharacter ComponentCharacter = null!;

    private ButtonWidget _creativeFlyButtonWidget = null!;

    private ButtonWidget _crouchButtonWidget = null!;

    private ButtonWidget _editItemButton = null!;

    private ButtonWidget _fogButtonWidget = null!;

    private GamepadHelpDialog? _gamepadHelpDialog;

    private bool _gamepadHelpMessageShown;

    private ButtonWidget _helpButtonWidget = null!;

    private readonly List<(BitmapButtonWidget Button, string AttachedName, string FloatingName)>
        _hudSafeAreaButtons = [];

    private bool? _hudSafeAreaEnabled;

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

    private ToastWidget _messageWidget = null!;

    private ModalPanelAnimationData? _modalPanelAnimationData;

    private int _modalPanelOpenedFrame = -1;

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

    public bool AreTouchControlsVisible { get; private set; }

    public float TouchControlsVisibilityFactor => 1f - _sidePanelsFactor;

    public float TouchControlsHorizontalInset =>
        Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled
            ? HudSafeAreaManager.GetEffectivePadding(
                  ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.GlobalScale
              ) +
              76f
            : 64f;

    public bool IsGameInputCaptured =>
        ModalPanelWidget is IGameInputCapturingWidget ||
        DialogsManager.HasDialogs(
            ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget
        );

    public Widget? ModalPanelWidget
    {
        get => _modalPanelContainerWidget.Children.Count <= 0 ? null : _modalPanelContainerWidget.Children[0];
        set
        {
            if (value == ModalPanelWidget)
            {
                return;
            }

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
                _modalPanelOpenedFrame = Time.FrameIndex;
            }

            UpdateModalPanelAnimation();
            ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.Input.Clear();
            ComponentCharacter.Entity.FindComponent<ComponentInput>(true)!.SetSplitSourceInventoryAndSlot(null, -1);
        }
    }

    public int[] DrawOrders => [9];

    public void Draw(Camera camera, int drawOrder)
    {
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (ComponentCharacter is not { IsLocallyControlled: true })
        {
            return;
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
        DisplaySmallMessage(MessageContent.Plain(text), color, blinking, playNotificationSound);
    }

    public void DisplaySmallMessage(
        MessageContent content,
        Color color,
        bool blinking,
        bool playNotificationSound
    )
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        _messageWidget.DisplayMessage(content, color, blinking);
        PlayMessageNotification(playNotificationSound);
    }

    private void PlayMessageNotification(bool playNotificationSound)
    {
        if (playNotificationSound && ComponentCharacter.CharacterData.IsLocalCharacter)
        {
            _subsystemAudio.PlaySound("Audio/UI/Message", 1f, 0f, ComponentCharacter.ComponentBody.Position, 2f, false);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        SubsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        ComponentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        AreTouchControlsVisible = ClientPlatform.Kind is Platform.Android;
        _sidePanelsFactor = AreTouchControlsVisible ? 0f : 1f;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemWeather = Project.FindSubsystem<SubsystemWeather>(true)!;
        var guiWidget = ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget;
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
        ViewWidget = ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.ViewWidget;
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
        _hudSafeAreaButtons.AddRange(
            [
                (guiWidget.Children.Find<BitmapButtonWidget>("BackButton")!, "BackButton", "BackButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("ClothingButton")!, "ClothingButton",
                    "ClothingButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("InventoryButton")!, "InventoryButton",
                    "InventoryButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("CharacterListButton")!, "CharacterList",
                    "CharacterListFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("MoreButton")!, "MoreButton", "MoreButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("CreativeFlyButton")!, "CreativeFlyButton",
                    "CreativeFlyButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("CrouchButton")!, "SneakButton",
                    "SneakButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("MountButton")!, "MountButton", "MountButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("EditItemButton")!, "EditItemButton",
                    "EditItemButtonFloating"),
                (guiWidget.Children.Find<BitmapButtonWidget>("MsgButton")!, "Msg", "MsgFloating")
            ]
        );
        ShortInventoryWidget = guiWidget.Children.Find<ShortInventoryWidget>("ShortInventory")!;
        _largeMessageWidget = guiWidget.Children.Find<ContainerWidget>("LargeMessage")!;
        _messageWidget = guiWidget.Children.Find<ToastWidget>("Toast")!;
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
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        ShortInventoryWidget.AssignComponents(ComponentCharacter.ComponentMiner.Inventory);
    }

    public override void OnEntityRemoved()
    {
        if (WorldRuntimeContext.For(Project).HasPresentation)
        {
            ShortInventoryWidget.AssignComponents(null);
        }

        _message = null;
    }

    public override void Dispose()
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        ModalPanelWidget = null;
        _keyboardHelpDialog = null;
        ShortInventoryWidget.AssignComponents(null);
    }

    public void UpdateSidePanelsAnimation()
    {
        var num = MathUtils.Min(Time.FrameDuration, 0.1f);
        var num2 = AreTouchControlsVisible ? 0f : 1f;
        var x = num2 - _sidePanelsFactor;
        if (MathUtils.Abs(x) > 0.01f)
        {
            _sidePanelsFactor += MathUtils.Clamp(
                12f * MathUtils.PowSign(x, 0.75f) * num,
                0f - MathUtils.Abs(x),
                MathUtils.Abs(x)
            );
        }
        else
        {
            _sidePanelsFactor = num2;
        }

        var safeAreaPadding = HudSafeAreaManager.GetEffectivePadding(
            ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.GlobalScale
        );
        _leftControlsContainerWidget.RenderTransform = Matrix.CreateTranslation(
            (0f - (_leftControlsContainerWidget.ActualSize.X + safeAreaPadding)) * _sidePanelsFactor,
            0f,
            0f
        );
        _rightControlsContainerWidget.RenderTransform = Matrix.CreateTranslation(
            (_rightControlsContainerWidget.ActualSize.X + safeAreaPadding) * _sidePanelsFactor,
            0f,
            0f
        );
        _leftControlsContainerWidget.IsEnabled = AreTouchControlsVisible;
        _rightControlsContainerWidget.IsEnabled = AreTouchControlsVisible;
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
        var componentRider = ComponentCharacter.ComponentRider;
        var componentSleep = ComponentCharacter.ComponentSleep;
        var worldSettings = SubsystemGameInfo.WorldSettings;
        var gameMode = worldSettings.GameMode;
        UpdateHudSafeArea();
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
            var num = (float)MathUtils.Min(
                MathUtils.Saturate(2.0 * (realTime - _message.StartTime)),
                MathUtils.Saturate(2.0 * (_message.StartTime + _message.Duration - realTime))
            );
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

        ControlsContainerWidget.IsVisible =
            ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.CharacterView
                .IsReadyForPlaying &&
            ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.ActiveCamera
                .IsEntityControlEnabled &&
            componentSleep.SleepFactor <= 0f;
        _moveRectangleContainerWidget.IsVisible = AreTouchControlsVisible;
        _lookRectangleContainerWidget.IsVisible = AreTouchControlsVisible &&
                                                  (Configuration.ClientConfiguration.Preferences.LookControlMode !=
                                                   LookControlMode.EntireScreen ||
                                                   Configuration.ClientConfiguration.Preferences.MoveControlMode !=
                                                   MoveControlMode.Buttons);
        _lookPadContainerWidget.IsVisible = Configuration.ClientConfiguration.Preferences.LookControlMode !=
                                            LookControlMode.SplitTouch;
        MoveRoseWidget.IsDrawEnabled = AreTouchControlsVisible;
        _moreContentsWidget.IsVisible = AreTouchControlsVisible && _moreButtonWidget.IsChecked;
        HealthBarWidget.IsVisible = gameMode != GameMode.Creative;
        FoodBarWidget.IsVisible = gameMode != 0 && worldSettings.AreAdventureSurvivalMechanicsEnabled;
        TemperatureBarWidget.IsVisible = gameMode != 0 && worldSettings.AreAdventureSurvivalMechanicsEnabled;
        LevelLabelWidget.IsVisible = gameMode != 0 && worldSettings.AreAdventureSurvivalMechanicsEnabled;
        _creativeFlyButtonWidget.IsVisible = gameMode == GameMode.Creative;
        _timeOfDayButtonWidget.IsVisible = gameMode == GameMode.Creative;
        _lightningButtonWidget.IsVisible = gameMode == GameMode.Creative;
        _precipitationButtonWidget.IsVisible = gameMode == GameMode.Creative && worldSettings.AreWeatherEffectsEnabled;
        _fogButtonWidget.IsVisible = gameMode == GameMode.Creative && worldSettings.AreWeatherEffectsEnabled;
        _moveButtonsContainerWidget.IsVisible = Configuration.ClientConfiguration.Preferences.MoveControlMode ==
                                                MoveControlMode.Buttons;
        _movePadContainerWidget.IsVisible =
            Configuration.ClientConfiguration.Preferences.MoveControlMode == MoveControlMode.Pad;
        if (Configuration.ClientConfiguration.Preferences.LeftHandedLayout)
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
        _crouchButtonWidget.IsChecked = ComponentCharacter.ComponentBody.TargetCrouchFactor > 0f;
        _creativeFlyButtonWidget.IsChecked = ComponentCharacter.ComponentLocomotion.IsCreativeFlyEnabled;
        _inventoryButtonWidget.IsChecked = IsInventoryVisible();
        _clothingButtonWidget.IsChecked = IsClothingVisible();
        if (IsActiveSlotEditable() ||
            ComponentCharacter.Entity.FindComponent<ComponentBlockHighlight>(true)!.NearbyEditableCell.HasValue)
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
                _lastResolvedContextAction =
                    Project.FindSubsystem<SubsystemClientModServices>(true)!.ContextActions.Resolve(
                        new PlayerContextActionQueryContext(ComponentCharacter)
                    );
                if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

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

        if (!ComponentCharacter.IsAddedToProject ||
            ComponentCharacter.ComponentHealth.Health == 0f ||
            componentSleep.IsSleeping ||
            ComponentCharacter.ComponentSickness.IsPuking)
        {
            ModalPanelWidget = null;
        }

        if (ComponentCharacter.ComponentSickness.IsSick)
        {
            ComponentCharacter.Entity.FindComponent<ComponentGui>(true)!.HealthBarWidget.LitBarColor =
                new Color(166, 175, 103);
        }
        else
        {
            ComponentCharacter.Entity.FindComponent<ComponentGui>(true)!.HealthBarWidget.LitBarColor =
                ComponentCharacter.ComponentFlu.HasFlu
                    ? new Color(0, 48, 255)
                    : new Color(224, 24, 0);
        }
    }

    private void UpdateHudSafeArea()
    {
        var enabled = Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled;
        var padding = HudSafeAreaManager.GetEffectivePadding(
            ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.GlobalScale
        );
        var sideMargin = new Vector2(padding, 0f);
        _leftControlsContainerWidget.Margin = sideMargin;
        _rightControlsContainerWidget.Margin = sideMargin;
        _moveContainerWidget.Margin = sideMargin;
        _lookContainerWidget.Margin = sideMargin;
        ShortInventoryWidget.AreSideControlsVisible = AreTouchControlsVisible;
        ShortInventoryWidget.HorizontalSafeAreaPadding = AreTouchControlsVisible ? padding : 0f;
        if (_hudSafeAreaEnabled == enabled)
        {
            return;
        }

        _hudSafeAreaEnabled = enabled;
        var touchControllerName = enabled ? "TouchControllerFloating" : "TouchController";
        var touchControllerSubtexture =
            TextureAtlasManager.GetSubtexture($"Textures/Atlas/{touchControllerName}");
        _moveRectangleWidget.Subtexture = touchControllerSubtexture;
        _lookRectangleWidget.Subtexture = touchControllerSubtexture;
        foreach (var (button, attachedName, floatingName) in _hudSafeAreaButtons)
        {
            var resourceName = enabled ? floatingName : attachedName;
            button.NormalSubtexture = TextureAtlasManager.GetSubtexture($"Textures/Atlas/{resourceName}");
            button.ClickedSubtexture =
                TextureAtlasManager.GetSubtexture($"Textures/Atlas/{resourceName}_Pressed");
            button.Size = enabled ? new Vector2(68f, 64f) : new Vector2(64f, 64f);
            button.Margin = enabled ? new Vector2(4f, 3f) : new Vector2(0f, 3f);
        }
    }

    public void HandleInput()
    {
        var input = ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.Input;
        var playerInput = ComponentCharacter.Entity.FindComponent<ComponentInput>(true)!.PlayerInput;
        var componentRider = ComponentCharacter.ComponentRider;

        if (playerInput.ToggleTouchControls)
        {
            AreTouchControlsVisible = !AreTouchControlsVisible;
            if (!AreTouchControlsVisible)
            {
                _moreButtonWidget.IsChecked = false;
            }
        }

        if (ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.ActiveCamera
            .IsEntityControlEnabled)
        {
            if (!_keyboardHelpMessageShown &&
                (ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.CharacterView
                     .InputDevice &
                 WidgetInputDevice.Keyboard) !=
                0 &&
                Time.PeriodicEvent(7.0, 0.0))
            {
                _keyboardHelpMessageShown = true;
                DisplaySmallMessage(Localization.ClientLanguage.Catalog.Get(TypeName, 1), Color.White, true, true);
            }
            else if (!_gamepadHelpMessageShown &&
                     (ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.CharacterView
                          .InputDevice &
                      WidgetInputDevice.Gamepads) !=
                     0 &&
                     Time.PeriodicEvent(7.0, 0.0))
            {
                _gamepadHelpMessageShown = true;
                DisplaySmallMessage(Localization.ClientLanguage.Catalog.Get(TypeName, 2), Color.White, true, true);
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
                DialogsManager.ShowDialog(
                    ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
                    _keyboardHelpDialog
                );
            }
        }

        if (playerInput.TogglePlayerPanel)
        {
            ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
                .TogglePlayerPanel();
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
                DialogsManager.ShowDialog(
                    ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
                    _gamepadHelpDialog
                );
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
                ModalPanelWidget = ComponentCharacter.ComponentMiner.Inventory is ComponentCreativeInventory
                    ? new CreativeInventoryWidget(ComponentCharacter.Entity)
                    : new FullInventoryWidget(
                        ComponentCharacter.ComponentMiner.Inventory,
                        ComponentCharacter.Entity.FindComponent<ComponentCraftingTable>(true)!
                    );
            }
        }

        if (playerInput.ToggleClothing || _clothingButtonWidget.IsClicked)
        {
            var clothing = new ClothingWidget(ComponentCharacter);
            ModalPanelWidget = IsClothingVisible() ? null : clothing;
        }

        if (_crouchButtonWidget.IsClicked || playerInput.ToggleSneak)
        {
            if (ComponentCharacter.ComponentBody.TargetCrouchFactor == 0f)
            {
                if (ComponentCharacter.ComponentBody.StandingOnValue.HasValue)
                {
                    ComponentCharacter.ComponentBody.TargetCrouchFactor = 1f;
                    DisplaySmallMessage(
                        Localization.ClientLanguage.Catalog.Get(TypeName, 4),
                        Color.White,
                        false,
                        false
                    );
                }
            }
            else
            {
                ComponentCharacter.ComponentBody.TargetCrouchFactor = 0f;
                DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(TypeName, 3),
                    Color.White,
                    false,
                    false
                );
            }
        }

        if ((_mountButtonWidget.IsClicked || playerInput.ToggleMount))
        {
            var flag = componentRider.Mount != null;
            if (!flag && _lastResolvedContextAction != null)
            {
                try
                {
                    _lastResolvedContextAction.Execute(new PlayerContextActionExecutionContext(ComponentCharacter));
                }
                catch (Exception exception)
                {
                    Log.Error($"Mod context action failed: {exception}");
                }

                if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }
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
                        ? Localization.ClientLanguage.Catalog.Get(TypeName, 5)
                        : Localization.ClientLanguage.Catalog.Get(TypeName, 6),
                    Color.White,
                    false,
                    false
                );
            }
        }

        var nearbyEditableCell =
            ComponentCharacter.Entity.FindComponent<ComponentBlockHighlight>(true)!.NearbyEditableCell;
        if ((_editItemButton.IsClicked || playerInput.EditItem) && nearbyEditableCell.HasValue)
        {
            var value = nearbyEditableCell.Value;
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(value.X, value.Y, value.Z);
            var contents = Terrain.ExtractContents(cellValue);
            var editBlockContext = new BlockEditContext(value.X, value.Y, value.Z, cellValue, ComponentCharacter);
            WorldRuleHooks.For(Project).Invoke(editBlockContext);
            if (!WorldRuntimeContext.IsProjectActive(editBlockContext.Project)) { return; }

            if (editBlockContext is { Cancel: false, Handled: false })
            {
                var blockBehaviors =
                    _subsystemBlockBehaviors.GetBlockBehaviors(contents);
                for (var i = 0;
                     i < blockBehaviors.Length &&
                     !blockBehaviors[i]
                         .OnEditBlock(value.X, value.Y, value.Z, cellValue, ComponentCharacter);
                     i++)
                {
                }
            }
        }
        else if ((_editItemButton.IsClicked || playerInput.EditItem) && IsActiveSlotEditable())
        {
            var inventory = ComponentCharacter.ComponentMiner.Inventory;
            var activeSlotIndex = inventory.ActiveSlotIndex;
            var num = Terrain.ExtractContents(inventory.GetSlotValue(activeSlotIndex));
            if (Modding.Blocks.WorldContentBinding.For(Project).Catalog.Blocks[num].Editable)
            {
                var editInventoryContext =
                    new BlockEditInventoryItemContext(inventory, activeSlotIndex, ComponentCharacter);
                WorldRuleHooks.For(Project).Invoke(editInventoryContext);
                if (!WorldRuntimeContext.IsProjectActive(editInventoryContext.Project)) { return; }

                if (!editInventoryContext.Cancel && !editInventoryContext.Handled)
                {
                    var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num);
                    for (var i = 0;
                         i < blockBehaviors.Length &&
                         !blockBehaviors[i]
                             .OnEditInventoryItem(inventory, activeSlotIndex, ComponentCharacter);
                         i++)
                    {
                    }
                }
            }
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_creativeFlyButtonWidget.IsClicked || playerInput.ToggleCreativeFly) &&
            componentRider.Mount == null)
        {
            var isCreativeFlyEnabled = ComponentCharacter.ComponentLocomotion.IsCreativeFlyEnabled;
            ComponentCharacter.ComponentLocomotion.IsCreativeFlyEnabled = !isCreativeFlyEnabled;
            if (ComponentCharacter.ComponentLocomotion.IsCreativeFlyEnabled != isCreativeFlyEnabled)
            {
                if (ComponentCharacter.ComponentLocomotion.IsCreativeFlyEnabled)
                {
                    ComponentCharacter.ComponentLocomotion.JumpOrder = 1f;
                    DisplaySmallMessage(
                        Localization.ClientLanguage.Catalog.Get(TypeName, 7),
                        Color.White,
                        false,
                        false
                    );
                }
                else
                {
                    DisplaySmallMessage(
                        Localization.ClientLanguage.Catalog.Get(TypeName, 8),
                        Color.White,
                        false,
                        false
                    );
                }
            }

            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                NetworkSender.SendToObservers(
                    ComponentCharacter.Entity,
                    CharacterPackageBuilder.Capture(
                        ComponentCharacter,
                        ComponentCharacterPackage.CharacterAction.CreativeFlyChange
                    ),
                    ComponentCharacter.CharacterData.ControllerConnection
                );
            }
            else
            {
                NetworkSender.SendToServer(
                    WorldRuntimeContext.For(Project),
                    CharacterPackageBuilder.Capture(
                        ComponentCharacter,
                        ComponentCharacterPackage.CharacterAction.CreativeFlyChange
                    )
                );
            }
        }

        if (!ComponentCharacter.Entity.FindComponent<ComponentInput>(true)!.IsControlledByVr &&
            (_cameraButtonWidget.IsClicked || playerInput.SwitchCameraMode))
        {
            ChangeCameraMode();
        }

        if (_photoButtonWidget.IsClicked || playerInput.TakeScreenshot)
        {
            ScreenCaptureManager.CapturePhoto(
                delegate
                {
                    DisplaySmallMessage(
                        Localization.ClientLanguage.Catalog.Get(TypeName, 13),
                        Color.White,
                        false,
                        false
                    );
                },
                delegate
                {
                    DisplaySmallMessage(
                        Localization.ClientLanguage.Catalog.Get(TypeName, 14),
                        Color.White,
                        false,
                        false
                    );
                }
            );
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_lightningButtonWidget.IsClicked || playerInput.Lighting))
        {
            CommandGateway.Submit(
                ComponentCharacter.CharacterData,
                new TriggerPlayerLightningCommand()
            );
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_precipitationButtonWidget.IsClicked || playerInput.Precipitation))
        {
            CommandGateway.Submit(
                ComponentCharacter.CharacterData,
                new SetPrecipitationCommand(!_subsystemWeather.IsPrecipitationStarted)
            );
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_fogButtonWidget.IsClicked || playerInput.Fog))
        {
            CommandGateway.Submit(
                ComponentCharacter.CharacterData,
                new SetFogCommand(!_subsystemWeather.IsFogStarted)
            );
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            (_timeOfDayButtonWidget.IsClicked || playerInput.TimeOfDay))
        {
            CommandGateway.Submit(
                ComponentCharacter.CharacterData,
                new AdvanceWorldTimeCommand()
            );
        }

        if (ModalPanelWidget != null)
        {
            if (Time.FrameIndex != _modalPanelOpenedFrame &&
                (input.Cancel || input.Back || _backButtonWidget.IsClicked))
            {
                ModalPanelWidget = null;
            }
        }
        else if (input.Back || _backButtonWidget.IsClicked)
        {
            DialogsManager.ShowDialog(
                ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget,
                new GameMenuDialog(ComponentCharacter)
            );
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
        var inventory = ComponentCharacter.ComponentMiner.Inventory;
        var activeSlotIndex = inventory.ActiveSlotIndex;
        var num = Terrain.ExtractContents(inventory.GetSlotValue(activeSlotIndex));
        return Modding.Blocks.WorldContentBinding.For(Project).Catalog.Blocks[num].Editable;
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
        var gameWidget = ComponentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget;
        if (gameWidget.ActiveCamera is FppCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<TppCamera>()!;
            DisplaySmallMessage(Localization.ClientLanguage.Catalog.Get(TypeName, 9), Color.White, false, false);
        }
        else if (gameWidget.ActiveCamera is TppCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<OrbitCamera>()!;
            DisplaySmallMessage(Localization.ClientLanguage.Catalog.Get(TypeName, 10), Color.White, false, false);
        }
        else if (gameWidget.ActiveCamera is OrbitCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<FixedCamera>()!;
            DisplaySmallMessage(Localization.ClientLanguage.Catalog.Get(TypeName, 11), Color.White, false, false);
        }
        else
        {
            if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
                gameWidget.ActiveCamera is FixedCamera)
            {
                gameWidget.ActiveCamera = gameWidget.FindCamera<DebugCamera>()!;
                DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(TypeName, 19),
                    Color.White,
                    false,
                    false
                );
            }
            else
            {
                gameWidget.ActiveCamera = gameWidget.FindCamera<FppCamera>()!;
                DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(TypeName, 12),
                    Color.White,
                    false,
                    false
                );
            }
        }
    }
}

using System.Xml.Linq;

using Engine.Input;

using EntitySystem.Core;

using Game.Commands;
using Game.Messaging;
using Game.Network;

namespace Game.Widgets;

public class GameWidget : CanvasWidget
{
    private readonly BitmapButtonWidget _messageButton;

    private readonly CanvasWidget _controlsWidget;

    private readonly StackPanelWidget? _informationOverlaysContainer;

    private readonly CanvasWidget? _informationOverlaysSpacer;

    private readonly GameMessageService? _messageService;

    private Camera _activeCamera;

    private readonly List<Camera> _cameras = [];

    public readonly MessagePanelWidget? MessagePanel;

    public readonly CharacterInformationOverlayWidget? CharacterInformationOverlay;

    public readonly MultiplayerPanelWidget? PlayerPanel;

    public readonly MessageHistoryOverlayWidget? MessageHistoryOverlay;

    public readonly BitmapButtonWidget NetCharacterListButton;

    public SubsystemTime? SubsystemTime;

    public ViewWidget ViewWidget { get; set; }

    public ContainerWidget GuiWidget { get; set; }

    public int GameWidgetIndex { get; set; }

    public SubsystemGameWidgets SubsystemGameWidgets { get; set; }

    public CharacterData CharacterData { get; set; }

    public ReadOnlyList<Camera> Cameras => new(_cameras);

    public Camera ActiveCamera
    {
        get => _activeCamera;
        set
        {
            if (value == null || value.GameWidget != this)
            {
                throw new InvalidOperationException("Invalid camera.");
            }

            if (!IsCameraAllowed(value))
            {
                value = FindCamera<FppCamera>()!;
            }

            if (value == _activeCamera)
            {
                return;
            }

            var activeCamera = _activeCamera;
            _activeCamera = value;
            _activeCamera.Activate(activeCamera);
        }
    }

    public ComponentCreature? Target { get; set; }


    public GameWidget(CharacterData characterData, int gameViewIndex)
    {
        CharacterData = characterData;
        GameWidgetIndex = gameViewIndex;
        SubsystemGameWidgets = characterData.SubsystemGameWidgets;
        SubsystemTime = SubsystemGameWidgets.Project.FindSubsystem<SubsystemTime>();
        LoadContents(this, ContentManager.Get<XElement>("Widgets/GameWidget"));
        _messageButton = Children.Find<BitmapButtonWidget>("MsgButton")!;
        NetCharacterListButton = Children.Find<BitmapButtonWidget>("CharacterListButton")!;
        NetCharacterListButton.NormalSubtexture = TextureAtlasManager.GetSubtexture("Textures/Atlas/CharacterList");
        NetCharacterListButton.ClickedSubtexture =
            TextureAtlasManager.GetSubtexture("Textures/Atlas/CharacterList_Pressed");
        _messageButton.Text = "";
        _messageButton.IsVisible = false;
        NetCharacterListButton.IsVisible = false;
        ViewWidget = Children.Find<ViewWidget>("View")!;
        GuiWidget = Children.Find<ContainerWidget>("Gui")!;
        _controlsWidget = GuiWidget.Children.Find<CanvasWidget>("ControlsContainer")!;
        var hasMultiplayerMessaging =
            CommonLib.WorkType is not WorkType.Local &&
            CommonLib.Net.IsConnected;
        if (characterData.IsLocalCharacter)
        {
            _messageService = SubsystemGameWidgets.Messages;
            _messageService.ToastRequested += DisplayToast;
        }

        if (hasMultiplayerMessaging && characterData.IsLocalCharacter)
        {
            CharacterInformationOverlay = new CharacterInformationOverlayWidget(this);
            PlayerPanel = new MultiplayerPanelWidget(characterData, CharacterInformationOverlay);
            MessageHistoryOverlay = new MessageHistoryOverlayWidget(SubsystemGameWidgets.Messages)
            {
                HorizontalAlignment = WidgetAlignment.Near,
                VerticalAlignment = WidgetAlignment.Near,
                DisplayEnabled = SettingsManager.Current.ShowMessageHistoryOverlay
            };
            _informationOverlaysSpacer = new CanvasWidget
            {
                Size = new Vector2(0f, 12f),
                IsHitTestVisible = false
            };
            _informationOverlaysContainer = new StackPanelWidget
            {
                Direction = LayoutDirection.Vertical,
                HorizontalAlignment = WidgetAlignment.Near,
                VerticalAlignment = WidgetAlignment.Near,
                Margin = new Vector2(12f, 8f),
                IsHitTestVisible = false
            };
            _informationOverlaysContainer.Children.Add(CharacterInformationOverlay);
            _informationOverlaysContainer.Children.Add(_informationOverlaysSpacer);
            _informationOverlaysContainer.Children.Add(MessageHistoryOverlay);
            NetCharacterListButton.IsVisible = true;
            _messageButton.IsVisible = true;
            _controlsWidget.Children.Insert(0, _informationOverlaysContainer);
        }

        if (characterData.IsLocalCharacter)
        {
            MessagePanel = new MessagePanelWidget(
                characterData,
                MessageHistoryOverlay,
                hasMultiplayerMessaging
            );
        }

        _cameras.Add(new FppCamera(this));
        _cameras.Add(new DeathCamera(this));
        _cameras.Add(new IntroCamera(this));
        _cameras.Add(new TppCamera(this));
        _cameras.Add(new OrbitCamera(this));
        _cameras.Add(new FixedCamera(this));
        _cameras.Add(new LoadingCamera(this));
        _cameras.Add(new DebugCamera(this));
        _activeCamera = FindCamera<LoadingCamera>()!;
    }

    public T? FindCamera<T>(bool throwOnError = true) where T : Camera
    {
        var val = _cameras.FirstOrDefault(c => c is T);
        if (val is T result)
        {
            return result;
        }

        return throwOnError
            ? throw new InvalidOperationException($"Camera with type \"{typeof(T).Name}\" not found.")
            : null;
    }

    public bool IsEntityTarget(Entity entity)
    {
        if (Target != null)
        {
            return Target.Entity == entity;
        }

        return false;
    }

    public bool IsEntityFirstPersonTarget(Entity entity)
    {
        if (IsEntityTarget(entity))
        {
            return ActiveCamera is FppCamera;
        }

        return false;
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (CommonLib.WorkType != WorkType.Local)
        {
            IsVisible = CharacterData.IsLocalCharacter;
        }

        base.MeasureOverride(parentAvailableSize);
    }

    public override void Update()
    {
        var player = CharacterData.ComponentCharacter;
        if (player != null)
        {
            if (_informationOverlaysContainer != null)
            {
                var horizontalMargin = MathUtils.Lerp(
                    12f,
                    12f + player.ComponentGui.TouchControlsHorizontalInset,
                    player.ComponentGui.TouchControlsVisibilityFactor
                );
                if (_informationOverlaysContainer.Margin.X != horizontalMargin)
                {
                    _informationOverlaysContainer.Margin = new Vector2(horizontalMargin, 8f);
                }
            }

            _informationOverlaysSpacer?.IsVisible = CharacterInformationOverlay?.IsVisible == true &&
                                                    MessageHistoryOverlay?.IsVisible == true;

            if (player.ComponentSleep.IsSleeping &&
                _informationOverlaysContainer != null &&
                _controlsWidget.Children.Contains(_informationOverlaysContainer))
            {
                _controlsWidget.Children.Remove(_informationOverlaysContainer);
                GuiWidget.Children.Add(_informationOverlaysContainer);
            }

            if (!player.ComponentSleep.IsSleeping &&
                _informationOverlaysContainer != null &&
                GuiWidget.Children.Contains(_informationOverlaysContainer))
            {
                GuiWidget.Children.Remove(_informationOverlaysContainer);
                _controlsWidget.Children.Insert(0, _informationOverlaysContainer);
            }
        }

        if (NetCharacterListButton.IsClicked)
        {
            TogglePlayerPanel();
        }

        if (_messageButton.IsClicked && MessagePanel != null)
        {
            ToggleMessagePanel(false);
        }

        var modalPanel = CharacterData.ComponentCharacter?.ComponentGui.ModalPanelWidget;
        var isGameInputCaptured = CharacterData.ComponentCharacter?.ComponentGui.IsGameInputCaptured == true;
        if (Window.IsActive &&
            CharacterData.IsReadyForPlaying &&
            player?.ComponentHealth.Health > 0f &&
            !isGameInputCaptured &&
            modalPanel == null &&
            !DialogsManager.HasDialogs(player.GuiWidget))
        {
            if (Input.IsKeyDownOnce(Key.M))
            {
                CommandGateway.Submit(CharacterData, new MarkPrivatePositionCommand("previous"));
            }

            if (Input.IsKeyDownOnce(Key.F6))
            {
                CommandGateway.Submit(CharacterData, new TeleportMarkCommand("previous", false));
            }
        }

        if (!isGameInputCaptured &&
            Input.IsKeyDownOnce(Key.Enter) &&
            MessagePanel is { EditText.HasFocus: false } enterMessagePanel)
        {
            if (CharacterData.ComponentCharacter?.ComponentGui.ModalPanelWidget ==
                enterMessagePanel)
            {
                ToggleMessagePanel(false);
            }
            else
            {
                OpenMessagePanel(
                    focusInput: MessageHistoryOverlay != null,
                    commandInput: MessageHistoryOverlay == null
                );
            }

            Input.Clear();
        }

        if (!isGameInputCaptured &&
            Input.IsKeyDownOnce(Key.Tilde) &&
            MessagePanel is not null)
        {
            var currentModalPanel =
                CharacterData.ComponentCharacter?.ComponentGui.ModalPanelWidget;
            if (currentModalPanel == MessagePanel && MessagePanel.IsCommandInput)
            {
                ToggleMessagePanel(false);
                Input.Clear();
            }
            else if (!MessagePanel.EditText.HasFocus)
            {
                OpenMessagePanel(false, true);
                Input.Clear();
            }
        }

        if (MessagePanel != null && modalPanel != MessagePanel)
        {
            MessagePanel.EditText.HasFocus = false;
        }

        _messageButton.IsChecked = modalPanel == MessagePanel;
        NetCharacterListButton.IsChecked = modalPanel == PlayerPanel;

        var widgetInputDevice = DetermineInputDevices();
        if (WidgetsHierarchyInput == null || WidgetsHierarchyInput.Devices != widgetInputDevice)
        {
            WidgetsHierarchyInput = new WidgetInput(widgetInputDevice);
        }

        if (GuiWidget.ParentWidget == null)
        {
            UpdateWidgetsHierarchy(GuiWidget);
        }
    }

    private WidgetInputDevice DetermineInputDevices()
    {
        if ((CharacterData.SubsystemCharacters.Characters.Count > 0 && CharacterData.IsLocalCharacter) ||
            (CommonLib.WorkType == WorkType.Local && CharacterData == CharacterData.SubsystemCharacters.Characters[0]))
        {
            var widgetInputDevice = WidgetInputDevice.None;
            foreach (var playersDatum in CharacterData.SubsystemCharacters.Characters)
            {
                if (playersDatum != CharacterData)
                {
                    widgetInputDevice |= playersDatum.InputDevice;
                }
            }

            return (WidgetInputDevice.All & ~widgetInputDevice) | WidgetInputDevice.Touch | CharacterData.InputDevice;
        }

        var widgetInputDevice2 = WidgetInputDevice.None;
        foreach (var playersDatum2 in CharacterData.SubsystemCharacters.Characters)
        {
            if (playersDatum2 == CharacterData)
            {
                break;
            }

            widgetInputDevice2 |= playersDatum2.InputDevice;
        }

        return (CharacterData.InputDevice & ~widgetInputDevice2) | WidgetInputDevice.Touch;
    }

    public void TogglePlayerPanel()
    {
        if (PlayerPanel is null || CharacterData.ComponentCharacter?.ComponentGui is not { } gui)
        {
            return;
        }

        if (gui.ModalPanelWidget == PlayerPanel)
        {
            gui.ModalPanelWidget = null;
            return;
        }

        PlayerPanel.RefreshView();
        gui.ModalPanelWidget = PlayerPanel;
    }

    public void RefreshPlayerViews()
    {
        CharacterInformationOverlay?.RefreshView();
        PlayerPanel?.RefreshView();
    }

    public void ToggleMessagePanel(bool focusInput)
    {
        if (MessagePanel is null || CharacterData.ComponentCharacter?.ComponentGui is not { } gui)
        {
            return;
        }

        if (gui.ModalPanelWidget == MessagePanel)
        {
            MessagePanel.ResetInput();
            gui.ModalPanelWidget = null;
            return;
        }

        OpenMessagePanel(focusInput, false);
    }

    public void OpenMessagePanel(bool focusInput, bool commandInput)
    {
        if (MessagePanel is null || CharacterData.ComponentCharacter?.ComponentGui is not { } gui)
        {
            return;
        }

        MessagePanel.BeginPanelSession();
        gui.ModalPanelWidget = MessagePanel;
        MessagePanel.EditText.HasFocus = false;
        if (commandInput)
        {
            MessagePanel.BeginCommandInput();
        }
        else if (focusInput)
        {
            MessagePanel.FocusInput();
        }
    }

    public override void Dispose()
    {
        if (_messageService is not null)
        {
            _messageService.ToastRequested -= DisplayToast;
        }

        var messageWidget = MessagePanel;
        var playerPanelWidget = PlayerPanel;
        if (CharacterData.ComponentCharacter?.ComponentGui is { } gui &&
            (gui.ModalPanelWidget == messageWidget ||
             gui.ModalPanelWidget == playerPanelWidget))
        {
            gui.ModalPanelWidget = null;
            gui.EndModalPanelAnimation();
        }

        if (messageWidget is { ParentWidget: null })
        {
            messageWidget.Dispose();
        }

        if (playerPanelWidget is { ParentWidget: null })
        {
            playerPanelWidget.Dispose();
        }

        base.Dispose();
    }

    private void DisplayToast(GameMessage message)
    {
        if (CharacterData.ComponentCharacter?.ComponentGui is not { } gui)
        {
            return;
        }

        var color = message.Tone switch
        {
            GameMessageTone.Success => new Color(108, 218, 126),
            GameMessageTone.Error => new Color(255, 112, 112),
            GameMessageTone.Warning => new Color(245, 206, 96),
            _ => Color.White
        };
        var blinking = message.Kind is GameMessageKind.Sign ||
                       message.Tone is GameMessageTone.Error or GameMessageTone.Warning;
        gui.DisplaySmallMessage(
            message.Content,
            color,
            blinking,
            playNotificationSound: blinking
        );
    }

    private bool IsCameraAllowed(Camera camera)
    {
        if (!(CharacterData.ComponentCharacter?.ComponentInput.IsControlledByVr ?? false))
        {
            return true;
        }

        if (camera is not FppCamera && camera is not LoadingCamera)
        {
            return camera is DeathCamera;
        }

        return true;
    }
}

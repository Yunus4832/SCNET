using System.Xml.Linq;

using Engine.Input;

using EntitySystem.Core;

using Game.Application;
using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Messaging;

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

    public CharacterPresentation Presentation { get; }

    public CharacterData CharacterData => Presentation.CharacterData;

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


    public GameWidget(CharacterPresentation presentation, int gameViewIndex)
    {
        Presentation = presentation;
        var characterData = presentation.CharacterData;
        GameWidgetIndex = gameViewIndex;
        SubsystemGameWidgets = characterData.Project.FindSubsystem<SubsystemGameWidgets>(true)!;
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
            ClientApplication.World is { IsMultiplayer: true } world &&
            ReferenceEquals(world.Project, characterData.Project);
        if (characterData.IsLocalCharacter)
        {
            _messageService = characterData.Project.FindSubsystem<SubsystemMessages>(true)!.Service;
            _messageService.ToastRequested += DisplayToast;
            _commandResults = WorldCommandResults.For(characterData.Project);
            _commandResults.Delivered += ReceiveCommandResult;
        }

        if (hasMultiplayerMessaging && characterData.IsLocalCharacter)
        {
            CharacterInformationOverlay = new CharacterInformationOverlayWidget(this);
            PlayerPanel = new MultiplayerPanelWidget(characterData, CharacterInformationOverlay);
            MessageHistoryOverlay =
                new MessageHistoryOverlayWidget(characterData.Project.FindSubsystem<SubsystemMessages>(true)!.Service)
                {
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Near,
                    DisplayEnabled = Configuration.ClientConfiguration.Preferences.ShowMessageHistoryOverlay
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

    private bool _viewClosed;

    private WorldCommandResults? _commandResults;

    private bool _disposed;

    internal bool IsViewClosed => _viewClosed;

    internal bool IsViewActive =>
        !_viewClosed && !Presentation.IsDisposed && ReferenceEquals(Presentation.Widget, this);

    internal void CloseView()
    {
        _viewClosed = true;
        IsVisible = false;
        IsEnabled = false;
        if (GuiWidget is { } gui)
        {
            gui.IsVisible = false;
            gui.IsEnabled = false;
        }
    }

    public override void Draw(DrawContext dc)
    {
        if (!IsViewActive) { return; }

        base.Draw(dc);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (!IsViewActive)
        {
            DesiredSize = Vector2.Zero;
            return;
        }

        IsVisible = CharacterData.IsLocalCharacter;

        base.MeasureOverride(parentAvailableSize);
    }

    public override void Update()
    {
        if (!IsViewActive) { return; }

        var player = CharacterData.ComponentCharacter;
        if (player != null)
        {
            if (_informationOverlaysContainer != null)
            {
                var horizontalMargin = MathUtils.Lerp(
                    12f,
                    12f + player.Entity.FindComponent<ComponentGui>(true)!.TouchControlsHorizontalInset,
                    player.Entity.FindComponent<ComponentGui>(true)!.TouchControlsVisibilityFactor
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

        var modalPanel = CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget;
        var isGameInputCaptured =
            CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.IsGameInputCaptured == true;
        if (Window.IsActive &&
            Presentation.IsReadyForPlaying &&
            player?.ComponentHealth.Health > 0f &&
            !isGameInputCaptured &&
            modalPanel == null &&
            !DialogsManager.HasDialogs(player.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GuiWidget))
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
            if (CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget ==
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
                CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget;
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

        if (CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget is not null ||
            DialogsManager.HasDialogs(GuiWidget))
        {
            ReleaseDialogCursors(Input, ViewWidget.WidgetToScreen(ViewWidget.ActualSize / 2f));
        }

        if (GuiWidget.ParentWidget == null)
        {
            UpdateWidgetsHierarchy(GuiWidget);
        }
    }

    internal static void ReleaseDialogCursors(WidgetInput input, Vector2 center)
    {
        if (!input.IsMouseCursorVisible) { input.MousePosition = center; }

        input.IsMouseCursorVisible = true;
        if (!input.IsPadCursorVisible) { input.PadCursorPosition = center; }

        input.IsPadCursorVisible = true;
        input.IsVrCursorVisible = true;
    }

    private WidgetInputDevice DetermineInputDevices()
    {
        if (CharacterData.IsLocalCharacter)
        {
            var widgetInputDevice = WidgetInputDevice.None;
            foreach (var playersDatum in CharacterData.SubsystemCharacters.Characters)
            {
                if (playersDatum != CharacterData)
                {
                    widgetInputDevice |= CharacterData.Project.FindSubsystem<SubsystemCharacterPresentation>(true)!
                        .Get(playersDatum).InputDevice;
                }
            }

            return (WidgetInputDevice.All & ~widgetInputDevice) | WidgetInputDevice.Touch | Presentation.InputDevice;
        }

        var widgetInputDevice2 = WidgetInputDevice.None;
        foreach (var playersDatum2 in CharacterData.SubsystemCharacters.Characters)
        {
            if (playersDatum2 == CharacterData)
            {
                break;
            }

            widgetInputDevice2 |= CharacterData.Project.FindSubsystem<SubsystemCharacterPresentation>(true)!
                .Get(playersDatum2).InputDevice;
        }

        return (Presentation.InputDevice & ~widgetInputDevice2) | WidgetInputDevice.Touch;
    }

    public void TogglePlayerPanel()
    {
        if (PlayerPanel is null ||
            CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)! is not { } gui)
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
        if (MessagePanel is null ||
            CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)! is not { } gui)
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
        if (MessagePanel is null ||
            CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)! is not { } gui)
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
        if (_disposed) { return; }

        _disposed = true;
        CloseView();
        if (_messageService is not null)
        {
            _messageService.ToastRequested -= DisplayToast;
        }

        if (_commandResults is not null) { _commandResults.Delivered -= ReceiveCommandResult; }

        var messageWidget = MessagePanel;
        var playerPanelWidget = PlayerPanel;
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

    private void ReceiveCommandResult(CommandResultDelivery delivery)
    {
        if (!_disposed && !_viewClosed && CharacterData.IsLocalCharacter)
        {
            MessagePanel?.ReceiveCommandResult(delivery.Result, delivery.CorrelationId);
        }
    }

    private void DisplayToast(GameMessage message)
    {
        if (CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)! is not { } gui)
        {
            return;
        }

        var style = ResolveToastStyle(message);
        var color = ResolveToastColor(style);
        gui.DisplaySmallMessage(
            message.Content,
            color,
            style.Blinking,
            style.PlayNotificationSound
        );
    }

    internal static Color ResolveToastColor(GameMessageToastStyle style) => style.Tone switch
    {
        GameMessageTone.Success => new Color(108, 218, 126),
        GameMessageTone.Error => new Color(255, 112, 112),
        GameMessageTone.Warning => new Color(245, 206, 96),
        _ => Color.White
    };

    internal static GameMessageToastStyle ResolveToastStyle(GameMessage message)
    {
        if (message.ToastStyle is { } style) { return style; }

        var blinking = message.Kind is GameMessageKind.Sign ||
                       message.Tone is GameMessageTone.Error or GameMessageTone.Warning;
        return new GameMessageToastStyle(message.Tone, blinking, blinking);
    }

    private bool IsCameraAllowed(Camera camera)
    {
        if (!(CharacterData.ComponentCharacter?.Entity.FindComponent<ComponentInput>(true)!.IsControlledByVr ?? false))
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

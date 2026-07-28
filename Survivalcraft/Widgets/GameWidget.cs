using System.Xml.Linq;

using Engine.Graphics;
using Engine.Input;

using EntitySystem.Core;

using Game.Messaging;
using Game.Network;
using Game.Network.Enums;

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

    public readonly PlayerInformationOverlayWidget? PlayerInformationOverlay;

    public readonly PlayerPanelWidget? PlayerPanel;

    public readonly MessageHistoryOverlayWidget? MessageHistoryOverlay;

    public readonly BitmapButtonWidget NetPlayerListButton;

    public SubsystemTime? SubsystemTime;

    public ViewWidget ViewWidget { get; set; }

    public ContainerWidget GuiWidget { get; set; }

    public int GameWidgetIndex { get; set; }

    public SubsystemGameWidgets SubsystemGameWidgets { get; set; }

    public PlayerData PlayerData { get; set; }

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


    public GameWidget(PlayerData playerData, int gameViewIndex)
    {
        PlayerData = playerData;
        GameWidgetIndex = gameViewIndex;
        SubsystemGameWidgets = playerData.SubsystemGameWidgets;
        SubsystemTime = SubsystemGameWidgets.Project.FindSubsystem<SubsystemTime>();
        LoadContents(this, ContentManager.Get<XElement>("Widgets/GameWidget"));
        _messageButton = Children.Find<BitmapButtonWidget>("MsgButton")!;
        NetPlayerListButton = Children.Find<BitmapButtonWidget>("PlayerListButton")!;
        NetPlayerListButton.NormalSubtexture = LoadGuiSubtexture("Textures/Gui/PlayerList");
        NetPlayerListButton.ClickedSubtexture = LoadGuiSubtexture("Textures/Gui/PlayerList_Pressed");
        _messageButton.Text = "";
        _messageButton.IsVisible = false;
        NetPlayerListButton.IsVisible = false;
        ViewWidget = Children.Find<ViewWidget>("View")!;
        GuiWidget = Children.Find<ContainerWidget>("Gui")!;
        _controlsWidget = GuiWidget.Children.Find<CanvasWidget>("ControlsContainer")!;
        if (CommonLib.Net.IsConnected && playerData.IsMainPlayer)
        {
            _messageService = SubsystemGameWidgets.Messages;
            _messageService.ToastRequested += DisplayToast;
            PlayerInformationOverlay = new PlayerInformationOverlayWidget(this);
            PlayerPanel = new PlayerPanelWidget(playerData, PlayerInformationOverlay);
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
            _informationOverlaysContainer.Children.Add(PlayerInformationOverlay);
            _informationOverlaysContainer.Children.Add(_informationOverlaysSpacer);
            _informationOverlaysContainer.Children.Add(MessageHistoryOverlay);
            MessagePanel = new MessagePanelWidget(
                playerData,
                MessageHistoryOverlay);
            NetPlayerListButton.IsVisible = true;
            _messageButton.IsVisible = true;
            _controlsWidget.Children.Insert(0, _informationOverlaysContainer);
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
            IsVisible = PlayerData.IsMainPlayer;
        }

        base.MeasureOverride(parentAvailableSize);
    }

    public override void Update()
    {
        var player = PlayerData.ComponentPlayer;
        if (player != null)
        {
            if (_informationOverlaysContainer != null)
            {
                var horizontalMargin = player.ComponentInput.IsControlledByTouch ? 76f : 12f;
                if (_informationOverlaysContainer.Margin.X != horizontalMargin)
                {
                    _informationOverlaysContainer.Margin = new Vector2(horizontalMargin, 8f);
                }
            }

            if (_informationOverlaysSpacer != null)
            {
                _informationOverlaysSpacer.IsVisible =
                    PlayerInformationOverlay?.IsVisible == true &&
                    MessageHistoryOverlay?.IsVisible == true;
            }

            if (player.ComponentSleep.IsSleeping && _informationOverlaysContainer != null &&
                _controlsWidget.Children.Contains(_informationOverlaysContainer))
            {
                _controlsWidget.Children.Remove(_informationOverlaysContainer);
                GuiWidget.Children.Add(_informationOverlaysContainer);
            }

            if (!player.ComponentSleep.IsSleeping && _informationOverlaysContainer != null &&
                GuiWidget.Children.Contains(_informationOverlaysContainer))
            {
                GuiWidget.Children.Remove(_informationOverlaysContainer);
                _controlsWidget.Children.Insert(0, _informationOverlaysContainer);
            }
        }

        if (NetPlayerListButton.IsClicked)
        {
            TogglePlayerPanel();
        }

        if (_messageButton.IsClicked && MessagePanel != null)
        {
            ToggleMessagePanel(false);
        }

        if (Input.IsKeyDownOnce(Key.Enter) &&
            MessagePanel is { EditText.HasFocus: false } &&
            PlayerData.ComponentPlayer?.ComponentGui.ModalPanelWidget is not MessagePanelWidget)
        {
            OpenMessagePanel(true, false);
            Input.Clear();
        }

        if (Input.IsKeyDownOnce(Key.Slash) &&
            MessagePanel is { EditText.HasFocus: false })
        {
            OpenMessagePanel(false, true);
            Input.Clear();
        }

        var modalPanel = PlayerData.ComponentPlayer?.ComponentGui.ModalPanelWidget;
        if (MessagePanel != null && modalPanel != MessagePanel)
        {
            MessagePanel.EditText.HasFocus = false;
        }

        _messageButton.IsChecked = modalPanel == MessagePanel;
        NetPlayerListButton.IsChecked = modalPanel == PlayerPanel;

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

    private static Subtexture LoadGuiSubtexture(string name)
    {
        return new Subtexture(ContentManager.Get<Texture2D>(name), Vector2.Zero, Vector2.One);
    }

    private WidgetInputDevice DetermineInputDevices()
    {
        if ((PlayerData.SubsystemPlayers.PlayersData.Count > 0 && PlayerData.IsMainPlayer) ||
            (CommonLib.WorkType == WorkType.Local && PlayerData == PlayerData.SubsystemPlayers.PlayersData[0]))
        {
            var widgetInputDevice = WidgetInputDevice.None;
            foreach (var playersDatum in PlayerData.SubsystemPlayers.PlayersData)
            {
                if (playersDatum != PlayerData)
                {
                    widgetInputDevice |= playersDatum.InputDevice;
                }
            }

            return (WidgetInputDevice.All & ~widgetInputDevice) | WidgetInputDevice.Touch | PlayerData.InputDevice;
        }

        var widgetInputDevice2 = WidgetInputDevice.None;
        foreach (var playersDatum2 in PlayerData.SubsystemPlayers.PlayersData)
        {
            if (playersDatum2 == PlayerData)
            {
                break;
            }

            widgetInputDevice2 |= playersDatum2.InputDevice;
        }

        return (PlayerData.InputDevice & ~widgetInputDevice2) | WidgetInputDevice.Touch;
    }

    public void TogglePlayerPanel()
    {
        if (PlayerPanel is null || PlayerData.ComponentPlayer?.ComponentGui is not { } gui)
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
        PlayerInformationOverlay?.RefreshView();
        PlayerPanel?.RefreshView();
    }

    public void ToggleMessagePanel(bool focusInput)
    {
        if (MessagePanel is null || PlayerData.ComponentPlayer?.ComponentGui is not { } gui)
        {
            return;
        }

        if (gui.ModalPanelWidget == MessagePanel)
        {
            MessagePanel.EditText.HasFocus = false;
            gui.ModalPanelWidget = null;
            return;
        }

        OpenMessagePanel(focusInput, false);
    }

    public void OpenMessagePanel(bool focusInput, bool commandInput)
    {
        if (MessagePanel is null || PlayerData.ComponentPlayer?.ComponentGui is not { } gui)
        {
            return;
        }

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
        if (PlayerData.ComponentPlayer?.ComponentGui is { } gui &&
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
        if (PlayerData.ComponentPlayer?.ComponentGui is not { } gui)
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
        var blinking = message.Tone is GameMessageTone.Error or GameMessageTone.Warning;
        gui.DisplaySmallMessage(
            message.Content.PlainText,
            color,
            blinking,
            playNotificationSound: blinking);
    }

    private bool IsCameraAllowed(Camera camera)
    {
        if (!(PlayerData.ComponentPlayer?.ComponentInput.IsControlledByVr ?? false))
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

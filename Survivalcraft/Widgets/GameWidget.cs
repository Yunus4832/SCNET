using System.Xml.Linq;

using Engine.Input;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;

namespace Game.Widgets;

public class GameWidget : CanvasWidget
{
    private readonly BitmapButtonWidget _bitmapButtonWidget;

    private readonly CanvasWidget _controlsWidget;

    private readonly CanvasWidget _messageInputModalBlocker = new();

    private Camera _activeCamera;

    private readonly List<Camera> _cameras = [];

    public readonly NetMessageWidget? MessageWidget;

    public readonly NetPanelWidget? NetPanelWidget;

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
        _bitmapButtonWidget = Children.Find<BitmapButtonWidget>("MsgButton")!;
        NetPlayerListButton = Children.Find<BitmapButtonWidget>("PlayerlistButton")!;
        _bitmapButtonWidget.Text = "";
        ViewWidget = Children.Find<ViewWidget>("View")!;
        GuiWidget = Children.Find<ContainerWidget>("Gui")!;
        _controlsWidget = GuiWidget.Children.Find<CanvasWidget>("ControlsContainer")!;
        if (CommonLib.Net.IsConnected && playerData.IsMainPlayer)
        {
            NetPanelWidget = new NetPanelWidget(this);
            NetPlayerListButton.IsChecked = true;
            NetPanelWidget.Margin = new Vector2(68, 5);
            MessageWidget = new NetMessageWidget(playerData, NetPanelWidget) { IsVisible = false };
            _controlsWidget.Children.Insert(0, NetPanelWidget);
            _controlsWidget.Children.Add(MessageWidget);
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
            if (player.ComponentSleep.IsSleeping && NetPanelWidget != null &&
                _controlsWidget.Children.Contains(NetPanelWidget))
            {
                _controlsWidget.Children.Remove(NetPanelWidget);
                if (MessageWidget != null)
                {
                    _controlsWidget.Children.Remove(MessageWidget);
                    GuiWidget.Children.Add(NetPanelWidget);
                    GuiWidget.Children.Add(MessageWidget);
                }
            }

            if (!player.ComponentSleep.IsSleeping && NetPanelWidget != null &&
                GuiWidget.Children.Contains(NetPanelWidget))
            {
                GuiWidget.Children.Remove(NetPanelWidget);
                if (MessageWidget != null)
                {
                    GuiWidget.Children.Remove(MessageWidget);
                    _controlsWidget.Children.Insert(0, NetPanelWidget);
                    _controlsWidget.Children.Add(MessageWidget);
                }
            }
        }

        if (NetPlayerListButton.IsClicked)
        {
            if (NetPanelWidget == null)
            {
                return;
            }

            NetPanelWidget.IsVisible = !NetPanelWidget.IsVisible;
            NetPlayerListButton.IsChecked = NetPanelWidget.IsVisible;
        }

        if (_bitmapButtonWidget.IsClicked)
        {
            if (MessageWidget == null)
            {
                return;
            }

            SetMessageWidgetVisible(!MessageWidget.IsVisible, false);
        }

        if (Input.IsKeyDownOnce(Key.Tab))
        {
            if (MessageWidget == null)
            {
                return;
            }

            if (PlayerData is { ComponentPlayer.ComponentGui: not null })
            {
                SetMessageWidgetVisible(!MessageWidget.IsVisible, true);
            }
        }

        if (Input.IsKeyDownOnce(Key.Enter))
        {
            if (MessageWidget == null)
            {
                return;
            }

            if (MessageWidget.EditText.Text.Length == 0)
            {
                SetMessageWidgetVisible(!MessageWidget.IsVisible, true);
            }
        }


        if (MessageWidget is { IsVisible: false })
        {
            MessageWidget.EditText.HasFocus = false;
        }

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

    private void SetMessageWidgetVisible(bool visible, bool focusInput)
    {
        if (MessageWidget is null)
        {
            return;
        }

        MessageWidget.IsVisible = visible;
        if (!visible)
        {
            MessageWidget.EditText.HasFocus = false;
            Input.IsMouseCursorVisible = false;
        }
        else if (focusInput)
        {
            MessageWidget.EditText.HasFocus = visible;
            Input.IsMouseCursorVisible = visible;
        }

        if (PlayerData.ComponentPlayer is null)
        {
            return;
        }

        PlayerData.ComponentPlayer.ComponentInput.AllowHandleInput = !MessageWidget.EditText.HasFocus;
        SetMessageInputModalBlocker(visible);
    }

    private void SetMessageInputModalBlocker(bool visible)
    {
        if (PlatformManager.Platform is not Platform.Desktop)
        {
            return;
        }

        PlayerData.ComponentPlayer?.ComponentGui?.ModalPanelWidget = visible ? _messageInputModalBlocker : null;
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

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

    private Camera _activeCamera;

    private readonly List<Camera> _cameras = [];

    public readonly NetMessageWidget? MessageWidget;

    public readonly NetPanelWidget? NetPanelWidget;

    public readonly BitmapButtonWidget NetPlayerlistButton;

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
        NetPlayerlistButton = Children.Find<BitmapButtonWidget>("PlayerlistButton")!;
        _bitmapButtonWidget.Text = "";
        ViewWidget = Children.Find<ViewWidget>("View")!;
        GuiWidget = Children.Find<ContainerWidget>("Gui")!;
        _controlsWidget = GuiWidget.Children.Find<CanvasWidget>("ControlsContainer")!;
        if (CommonLib.Net.IsConnected && playerData.IsMainPlayer)
        {
            NetPanelWidget = new NetPanelWidget(this);
            NetPlayerlistButton.IsChecked = true;
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

        if (NetPlayerlistButton.IsClicked)
        {
            if (NetPanelWidget == null)
            {
                return;
            }

            NetPanelWidget.IsVisible = !NetPanelWidget.IsVisible;
            NetPlayerlistButton.IsChecked = NetPanelWidget.IsVisible;
        }

        if (_bitmapButtonWidget.IsClicked)
        {
            if (MessageWidget == null)
            {
                return;
            }

            MessageWidget.IsVisible = !MessageWidget.IsVisible;
            if (PlayerData is { ComponentPlayer.ComponentGui: not null })
            {
#if DESKTOP
                PlayerData.ComponentPlayer.ComponentGui.ModalPanelWidget =
                    MessageWidget.IsVisible ? new CanvasWidget() : null;
#endif
            }
        }

        if (Input.IsKeyDownOnce(Key.Tab))
        {
            if (MessageWidget == null)
            {
                return;
            }

            if (PlayerData is { ComponentPlayer.ComponentGui: not null })
            {
                MessageWidget.IsVisible = !MessageWidget.IsVisible;
                MessageWidget.EditText.HasFocus = MessageWidget.IsVisible;
                Input.IsMouseCursorVisible = MessageWidget.IsVisible;
                PlayerData.ComponentPlayer.ComponentInput.AllowHandleInput = !MessageWidget.EditText.HasFocus;
#if DESKTOP
                PlayerData.ComponentPlayer.ComponentGui.ModalPanelWidget =
                    MessageWidget.IsVisible ? new CanvasWidget() : null;
#endif
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
                MessageWidget.IsVisible = !MessageWidget.IsVisible;
                MessageWidget.EditText.HasFocus = MessageWidget.IsVisible;
                Input.IsMouseCursorVisible = MessageWidget.IsVisible;
                PlayerData.ComponentPlayer?.ComponentInput.AllowHandleInput = !MessageWidget.EditText.HasFocus;
#if DESKTOP
                PlayerData.ComponentPlayer?.ComponentGui?.ModalPanelWidget =
                    MessageWidget.IsVisible ? new CanvasWidget() : null;
#endif
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

    private bool IsCameraAllowed(Camera camera)
    {
        if (!(PlayerData.ComponentPlayer?.ComponentInput.IsControlledByVr ?? false))
        {
            return true;
        }

        if (camera is not FppCamera && !(camera is LoadingCamera))
        {
            return camera is DeathCamera;
        }

        return true;
    }
}

using System.Xml.Linq;

using Game.Widgets.Services;

namespace Game.Screens;

public sealed class ManageServicesScreen : Screen
{
    private readonly CanvasWidget _panels;

    private readonly Dictionary<ServiceKind, ServicePanelWidget> _pages = [];

    private ServiceKind _kind;

    private ServicePanelWidget? _active;

    private readonly Dictionary<ServiceKind, ButtonWidget> _tabs = [];

    public ManageServicesScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ManageServicesScreen"));
        _panels = Children.Find<CanvasWidget>("Panels")!;
        foreach (var kind in Enum.GetValues<ServiceKind>())
        {
            _tabs.Add(kind, Children.Find<ButtonWidget>(kind.ToString())!);
        }
    }

    public override void Enter(object[] parameters)
    {
        Select(_kind);
    }

    public override void Leave()
    {
        _active?.Hide();
        _active = null;
    }

    public override void Dispose()
    {
        Leave();
        base.Dispose();
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        foreach (var (kind, tab) in _tabs)
        {
            tab.IsChecked = kind == _kind;
            if (tab.IsClicked) { Select(kind); }
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private void Select(ServiceKind kind)
    {
        if (_active is not null && kind == _kind) { return; }

        _active?.Hide();
        _kind = kind;
        if (!_pages.TryGetValue(kind, out var panel))
        {
            panel = kind switch
            {
                ServiceKind.ServerSource => new ServerSourceManagementPanel(),
                ServiceKind.Rendezvous => new RendezvousManagementPanel(),
                ServiceKind.Authentication => new AuthenticationManagementPanel(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            _pages.Add(kind, panel);
            _panels.Children.Add(panel);
        }

        _active = panel;
        panel.Show();
    }
}

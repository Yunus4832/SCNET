using System.Xml.Linq;

using Game.Widgets.Services;

namespace Game.Screens;

public sealed class ServiceCatalogScreen : Screen
{
    private readonly CanvasWidget _panels;

    private readonly Dictionary<ServiceKind, ServicePanelWidget> _pages = [];

    private ServiceKind _kind;

    private ServicePanelWidget? _active;

    private readonly SelectionDrawerWidget _type;

    public ServiceCatalogScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ServiceCatalogScreen"));
        _panels = Children.Find<CanvasWidget>("Panels")!;
        _type = Children.Find<SelectionDrawerWidget>("ServiceType")!;
        _type.ItemTextProvider = item => LanguageManager.GetContentWidgets("Services", item.ToString()!);
        _type.SetItems(Enum.GetValues<ServiceKind>().Cast<object>());
        _type.SelectionChanged += () =>
        {
            if (_type.SelectedItem is ServiceKind kind) { Select(kind); }
        };
    }

    public override void Enter(object[] parameters)
    {
        _type.RefreshItems();
        _type.SelectedItem = _kind;
        Select(_kind);
    }

    public override void Leave()
    {
        _active?.Hide();
        _active = null;
        _type.Close();
    }

    public override void Dispose()
    {
        Leave();
        base.Dispose();
    }

    public override void Update()
    {
        GameManager.UpdateProject();
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
                ServiceKind.ServerSource => new ServerSourceCatalogPanel(),
                ServiceKind.Rendezvous => new RendezvousCatalogPanel(),
                ServiceKind.Authentication => new AuthenticationCatalogPanel(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            _pages.Add(kind, panel);
            _panels.Children.Add(panel);
        }

        _active = panel;
        panel.Show();
    }
}

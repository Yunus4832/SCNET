using System.Xml.Linq;

namespace Game.Screens;

public class ContentScreen : Screen
{
    private const string _typeName = nameof(ContentScreen);

    private readonly ButtonWidget _manageButton;

    private readonly ButtonWidget _modsButton;

    private readonly ButtonWidget _remoteButton;

    private readonly ButtonWidget _packagesButton;

    private readonly ButtonWidget _repositoriesButton;

    private readonly ButtonWidget _serverSourcesButton;

    private readonly ButtonWidget _manageServerSourcesButton;

    public ContentScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ContentScreen");
        LoadContents(this, node);
        _modsButton = Children.Find<ButtonWidget>("Mods")!;
        _manageButton = Children.Find<BevelledButtonWidget>("Manage")!;
        _remoteButton = Children.Find<ButtonWidget>("Remote")!;
        _packagesButton = Children.Find<ButtonWidget>("Packages")!;
        _repositoriesButton = Children.Find<ButtonWidget>("Repositories")!;
        _serverSourcesButton = Children.Find<ButtonWidget>("ServerSources")!;
        _manageServerSourcesButton = Children.Find<ButtonWidget>("ManageServerSources")!;
    }

    public void OpenManageSelectDialog()
    {
        ScreensManager.SwitchScreen("ManageContent");
    }

    public override void Update()
    {
        if (_modsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ModManagement");
        }

        if (_remoteButton.IsClicked)
        {
            ScreensManager.SwitchScreen("OnlineContent");
        }

        if (_packagesButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ContentPackages");
        }

        if (_repositoriesButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ContentRepositories");
        }

        if (_serverSourcesButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ServerSources");
        }

        if (_manageServerSourcesButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ManageServerSources");
        }

        if (_manageButton.IsClicked)
        {
            OpenManageSelectDialog();
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("MainMenu");
        }
    }
}

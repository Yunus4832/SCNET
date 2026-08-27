using System.Xml.Linq;

namespace Game.Screens;

public class ContentScreen : Screen
{
    private const string _typeName = nameof(ContentScreen);

    private readonly ButtonWidget _manageButton;

    private readonly ButtonWidget _modsButton;

    private readonly ButtonWidget _remoteButton;

    public ContentScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ContentScreen");
        LoadContents(this, node);
        _modsButton = Children.Find<ButtonWidget>("Mods")!;
        _manageButton = Children.Find<BevelledButtonWidget>("Manage")!;
        _remoteButton = Children.Find<ButtonWidget>("Remote")!;
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
            ScreensManager.SwitchScreen("ContentServer");
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

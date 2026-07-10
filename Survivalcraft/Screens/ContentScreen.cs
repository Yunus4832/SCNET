using System.Xml.Linq;

namespace Game.Screens;

public class ContentScreen : Screen
{
    private const string _typeName = nameof(ContentScreen);

    private readonly ButtonWidget _communityContentButton;

    private readonly ButtonWidget _externalContentButton;

    private bool _isAdmin;

    private readonly ButtonWidget _linkButton;

    private readonly ButtonWidget _manageButton;

    private readonly ButtonWidget _modsButton;

    public ContentScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ContentScreen");
        LoadContents(this, node);
        _externalContentButton = Children.Find<ButtonWidget>("External")!;
        _communityContentButton = Children.Find<ButtonWidget>("Community")!;
        _linkButton = Children.Find<ButtonWidget>("Link")!;
        _modsButton = Children.Find<ButtonWidget>("Mods")!;
        _manageButton = Children.Find<BevelledButtonWidget>("Manage")!;
    }

    public override void Enter(object[] parameters)
    {
        base.Enter(parameters);
        CommunityContentManager.IsAdmin(
            new CancellableProgress(),
            delegate(bool isAdmin) { _isAdmin = isAdmin; },
            delegate { }
        );
    }

    public void OpenManageSelectDialog()
    {
        if (!_isAdmin)
        {
            ScreensManager.SwitchScreen("ManageContent");
            return;
        }

        var list = new List<string>
        {
            LanguageManager.Get(_typeName, 2),
            LanguageManager.Get(_typeName, 14)
        };

        DialogsManager.ShowDialog(null,
            new ListSelectionDialog(
                string.Empty,
                list,
                70f,
                item => (string)item,
                delegate(object item)
                {
                    var selectionResult = (string)item;
                    if (selectionResult == LanguageManager.Get(_typeName, 2))
                    {
                        ScreensManager.SwitchScreen("ManageContent");
                    }
                    else
                    {
                        ScreensManager.SwitchScreen("ManageUser");
                    }
                }
            )
        );
    }

    public override void Update()
    {
        _communityContentButton.IsEnabled = SettingsManager.Current.CommunityContentMode != CommunityContentMode.Disabled;
        if (_externalContentButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ExternalContent");
        }

        if (_communityContentButton.IsClicked)
        {
            ScreensManager.SwitchScreen("CommunityContent");
        }

        if (_linkButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new DownloadContentFromLinkDialog());
        }

        if (_modsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("ModManagement");
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
